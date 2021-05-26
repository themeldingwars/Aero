using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class Genv2
    {
        public    GeneratorExecutionContext Context;
        public    AeroGenConfig             Config;
        public    AeroSyntaxReceiver        SyntaxReceiver;
        protected List<string>              UsingsToAdd = new List<string>();
        public    StringBuilder             Sb          = new StringBuilder();

        protected int IndentLevel = 0;

        public Genv2(GeneratorExecutionContext context, AeroGenConfig config)
        {
            Context        = context;
            Config         = config;
            SyntaxReceiver = (AeroSyntaxReceiver) context.SyntaxReceiver;

            if (Config.DiagLogging) UsingsToAdd.Add("System.Collections.Generic");
            UsingsToAdd.Add("System");
            UsingsToAdd.Add("System.Buffers.Binary");
            UsingsToAdd.Add("System.Runtime.InteropServices");
            UsingsToAdd.Add("System.Text");
            UsingsToAdd.Add("System.Numerics");
        }

        public class AeroTypeHandler
        {
            public int                          Size = 0;
            public Func<string, string, string> Reader;
            public Func<string, string, string, string> Writer;
        }

        public static Dictionary<string, AeroTypeHandler> TypeHandlers = new()
        {
            {
                "byte", new AeroTypeHandler
                {
                    Size   = 1,
                    Reader = (name, typeCast) => $"{name} = {typeCast}data[offset];",
                    Writer = (name, typeStr, typeCast) => $"buffer[offset] = {typeCast}{name};",
                }
            },
            {
                "char", new AeroTypeHandler
                {
                    Size   = 1,
                    Reader = (name, typeCast) => $"{name} = ({typeCast}(char)data[offset]);",
                    Writer = (name, typeStr, typeCast) => $"buffer[offset] = {typeCast}((byte){name});",
                }
            },
            {
                "int", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, 4), {name});"
                }
            },
            {
                "uint", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), {name});"
                }
            },
            {
                "short", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, 2));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(offset, 2), {name});"
                }
            },
            {
                "ushort", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), {name});"
                }
            },
            {
                "long", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset, 8), {name});"
                }
            },
            {
                "ulong", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset, 8), {name});"
                }
            },
            {
                "float", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset, 4), {name});"
                }
            },
            {
                "double", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeStr, typeCast) => $"BinaryPrimitives.WriteDoubleLittleEndian(buffer.Slice(offset, 8), {name});"
                }
            },
            {
                "system.numerics.vector2", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name}.X = MemoryMarshal.Read<float>(data.Slice(offset, 4));" +
                                                                         $"{name}.Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4));",
                    Writer = (name, typeStr, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
                                                                         $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);"
                }
            },
            {
                "system.numerics.vector3", new AeroTypeHandler
                {
                    Size   = 12,
                    Reader = (name, typeCast) => $"{name}.X = MemoryMarshal.Read<float>(data.Slice(offset, 4));" +
                                                          $"{name}.Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4));" +
                                                          $"{name}.Z = MemoryMarshal.Read<float>(data.Slice(offset + 8, 4));",
                    Writer = (name, typeStr, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
                                                          $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);" +
                                                          $"MemoryMarshal.Write(buffer.Slice(offset + 8, sizeof(float)), ref {name}.Z);"
                }
            },
            {
                "system.numerics.vector4", new AeroTypeHandler
                {
                    Size   = 16,
                    Reader = (name, typeCast) => $"{name}.X = MemoryMarshal.Read<float>(data.Slice(offset, 4));" +
                                                          $"{name}.Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4));" +
                                                          $"{name}.Z = MemoryMarshal.Read<float>(data.Slice(offset + 8, 4));" +
                                                          $"{name}.W = MemoryMarshal.Read<float>(data.Slice(offset + 12, 4));",
                    Writer = (name, typeStr, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
                                                          $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);" +
                                                          $"MemoryMarshal.Write(buffer.Slice(offset + 8, sizeof(float)), ref {name}.Z);" +
                                                          $"MemoryMarshal.Write(buffer.Slice(offset + 12, sizeof(float)), ref {name}.W);"
                }
            },
            {
                "system.numerics.quaternion", new AeroTypeHandler
                {
                    Size   = 16,
                    Reader = (name, typeCast) => TypeHandlers["system.numerics.vector4"].Reader(name, typeCast),
                    Writer = (name, typeStr, typeCast) => TypeHandlers["system.numerics.vector4"].Writer(name, typeStr, typeCast)
                }
            }
        };

        public void AddReader(string name, string typeStr, string castTypeStr = null)
        {
            if (TypeHandlers.TryGetValue(typeStr.ToLower(), out AeroTypeHandler handler)) {
                var castedTypeStr = castTypeStr != null ? $"({castTypeStr})" : "";
                AddLine($"{handler.Reader(name, castedTypeStr)}");
                AddLine($"offset += {handler.Size};");
                AddLine();
            }
        }

        public static int GetTypeSize(string typeStr)
        {
            if (TypeHandlers.TryGetValue(typeStr.ToLower(), out AeroTypeHandler handler)) {
                return handler.Size;
            }

            return -1;
        }

    #region Code adding functions

        protected static int TabSpaces = 4;
        public void Indent() => IndentLevel += TabSpaces;
        public void UnIndent() => IndentLevel -= TabSpaces;
        public void AddLine(string line) => Sb.AppendLine($"{new string(' ', Math.Max(IndentLevel, 0))}{line}");
        public void AddLine() => AddLine("");
        public void AddLines(params string[] lines) => Array.ForEach(lines, AddLine);

    #endregion

    #region Code BLock creators

        public AgBlock Block(string blockStr) => new(this,
            () => AddLine(blockStr));

        public AgBlock Namespace(string nameSpaceName) => new(this,
            () => AddLine($"namespace {nameSpaceName}"));

        public AgBlock Class(string className) => new(this,
            () => AddLine($"public partial class {className}"));

        public AgBlock Function(string func) => new(this,
            () => AddLine(func));

        public AgBlock If(string ifStr) => new(this,
            () => AddLine($"if ({ifStr}) {{"), noOpenBracket: true);

        public AgBlock ForLen(string typeLen, string length, string indexName = "i") => new(this,
            () => AddLine($"for({typeLen} {indexName} = 0; {indexName} < {length}; {indexName}++)"));

        public void StartScope()
        {
            AddLine("{");
            Indent();
        }

        public void EndScope(bool noTrailingNewLine = false)
        {
            UnIndent();
            AddLine("}");
            if (noTrailingNewLine) AddLine();
        }

        public AgBlock AddBoundsCheck(string fieldName, string typeStr)
        {
            // Chars are 2 bytes in c# for unicode, we don't want that :>
            var sizeOfTypeStr = typeStr == "char" ? "byte" : typeStr;

            var sizeStr = GetTypeSize(sizeOfTypeStr);

            var ifStatment = @$"if (data.Length < (offset + {sizeStr}))";
            return new AgBlock(this, () =>
            {
                if (Config.BoundsCheck) {
                    if (Config.DiagLogging) {
                        AddLine($"{ifStatment} {{");
                        Indent();
                        {
                            AddLines(
                                @$"LogDiag($""Failed to read {typeStr}({{{sizeStr}}} bytes) for {fieldName}, offset: {{offset}} data length: {{data.Length}}, read overflowed by {{(offset + {sizeStr}) - data.Length}}."");",
                                "return -offset;");
                        }
                        UnIndent();
                        AddLine("}");
                    }
                    else {
                        AddLine($"{ifStatment} return -offset;");
                    }
                }
            }, () =>
            {
                if (Config.DiagLogging) {
                    AddDiagLog(
                        $"Read {typeStr}({{{sizeStr}}} bytes) for {fieldName} at offset {{offset - {sizeStr}}}.");
                }
            }, defaultAddNoContent: true);
        }

        public AgBlock AddBoundsCheckKnownLength(string fieldName, string typeStr, string length)
        {
            var ifStatment = @$"if (data.Length < (offset + {length}))";
            return new AgBlock(this, () =>
            {
                if (Config.BoundsCheck) {
                    if (Config.DiagLogging) {
                        AddLine($"{ifStatment} {{");
                        Indent();
                        {
                            AddLines(
                                @$"LogDiag($""Failed to read {typeStr}({length} bytes) for {fieldName}, offset: {{offset}} data length: {{data.Length}}, read overflowed by {{(offset + {length}) - data.Length}}."");",
                                "return -offset;");
                        }
                        UnIndent();
                        AddLine("}");
                    }
                    else {
                        AddLine($"{ifStatment} return -offset;");
                    }
                }
            }, () =>
            {
                if (Config.DiagLogging) {
                    AddDiagLog($"Read {typeStr}({length} bytes) for {fieldName} at offset {{offset - {length}}}.");
                }
            }, defaultAddNoContent: true);
        }

    #endregion

        public (string fileName, string source) GenClass(ClassDeclarationSyntax cd)
        {
            var fileName = $"{AgUtils.GetClassName(cd)}.Aero.cs";
            var ns       = AgUtils.GetNamespace(cd);

            AddLines(
                $"// Aero Generated file, not a not a good idea to edit :>",
                $"// {DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()}");
            AddUsings();
            AddLine();
            using (Namespace(ns)) {
                using (Class(AgUtils.GetClassName(cd))) {
                    if (Config.DiagLogging) AddDiagBoilerplate();

                #if DEBUG
                    AddLine("/*");
                    var treeRootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                    AddLine(AeroSourceGraphGen.PrintTree(treeRootNode));
                    AddLine("*/");
                #endif

                    CreateReaderV2(cd);
                    AddLine();
                    
                    CreateGetPackedSizeV2(cd);
                    AddLine();
                    
                    using (Function("public int Pack(Span<byte> data)")) {
                        AddLine("return 0;");
                    }

                    /*
                    CreateReader(cd);
                    AddLine();

                    CreateGetPackedSize(cd);
                    AddLine();

                    CreateWriter(cd);
                    AddLine();
                    */
                }
            }

            var sourceStr = Sb.ToString();
            return (fileName, sourceStr);
        }

        public virtual void AddDiagBoilerplate()
        {
            AddLines(
                "public List<string> DiagLogs = new List<string>();",
                "private void LogDiag(string msg) => DiagLogs.Add(msg);",
                ""
            );
        }

        public virtual void AddDiagLog(string msg) => AddLine(@$"LogDiag($""{msg}"");");

        public virtual void CreateReaderV2(ClassDeclarationSyntax cd)
        {
            using (Function("public int Unpack(ReadOnlySpan<byte> data)")) {
                AddLine("int offset = 0;");
                AddLine();
                
                CreateLogicFlow(cd, 
                preNode: node =>
                {
                    if (node is AeroArrayNode arrayNode) {
                        CreateForFromNode(arrayNode);
                    }
                },
                onNode: node =>
                {
                    if (node is AeroFieldNode fieldNode) {
                        AddReader(fieldNode.GetFullName(),
                            (fieldNode.IsEnum ? fieldNode.EnumStr : fieldNode.TypeStr).ToLower(),
                            fieldNode.IsEnum ? fieldNode.TypeStr : null);
                    }
                    else if (node is AeroStringNode stringNode) {
                        CreateStringReader(stringNode, node);
                    }
                });
                
                AddLine("return offset;");
            }
        }
        
        public virtual void CreateGetPackedSizeV2(ClassDeclarationSyntax cd)
        {
            using (Function("public int GetPackedSize()")) {
                AddLine("int offset = 0;");
                AddLine();
                
                var combinedSize = 0;
                CreateLogicFlow(cd, 
                    preNode: node =>
                    {
                        if (node is AeroArrayNode arrayNode && !arrayNode.IsFixedSize()) {
                            var idxName = $"idx{arrayNode.Depth}";
                                AddLine($"for (int {idxName} = 0; {idxName} < {arrayNode.GetFullName()}.Length; {idxName}++)");
                        }
                        
                        /*
                        if (node.IsFixedSize()) {
                            combinedSize += node.GetSize();
                        }

                        if (combinedSize > 0 && node is AeroIfNode) {
                            AddLine($"offset += {combinedSize}; // combined size");
                            combinedSize = 0;
                        }
                        */
                    },
                    onNode: node =>
                    {
                        if (node is AeroFieldNode fieldNode) {
                            var typeStr = fieldNode.IsEnum ? fieldNode.EnumStr.ToLower() : fieldNode.TypeStr.ToLower();
                            if (TypeHandlers.TryGetValue(typeStr, out AeroTypeHandler handler)) {
                                if (!node.Parent.IsRoot && node.Parent is AeroArrayNode farrayNode && farrayNode.Mode == AeroArrayNode.Modes.Fixed) {
                                    AddLine($"offset += {handler.Size * farrayNode.Length}; // array fixed");
                                }
                                else {
                                    AddLine($"offset += {handler.Size}; // field size");
                                }
                            }
                            else {
                                AddLine($"// {fieldNode.GetFullName()} had unknown type {typeStr}");
                            }
                        }
                        else if (node is AeroStringNode stringNode) {
                            int length = 0;
                            if (stringNode.Mode == AeroStringNode.Modes.LenTypePrefixed) {
                                length = GetTypeSize(stringNode.PrefixTypeStr);
                            }
                            else if (stringNode.Mode == AeroStringNode.Modes.NullTerminated) {
                                length = 1;
                            }

                            if (stringNode.Mode == AeroStringNode.Modes.Fixed) {
                                AddLine($"offset += {stringNode.GetSize()}; // string");
                            }
                            else {
                                AddLine($"offset += {length} + {stringNode.GetFullName()}.Length; // string");
                            }
                        }
                        else if (node is AeroArrayNode arrayNode && arrayNode.IsFixedSize()) {
                            var prefixLen = arrayNode.Mode == AeroArrayNode.Modes.LenTypePrefixed
                                ? GetTypeSize(arrayNode.PrefixTypeStr)
                                : 0;
                            
                            AddLine($"offset += ({prefixLen}) + ({arrayNode.GetSize()} * {arrayNode.GetFullName()}.Length); // array non fixed {node.Name}");
                            node.Nodes.Clear();
                        }
                        else if (node is AeroBlockNode && node.IsFixedSize()) {
                            AddLine($"offset += {node.GetSize()}; // Fixed size block");
                            node.Nodes.Clear();
                        }
                    });

                AddLine("return offset;");
            }
        }
        
        /*public virtual void CreateGetPackedSizeV2(ClassDeclarationSyntax cd)
        {
            using (Function("public int GetPackedSize()")) {
                AddLine("int offset = 0;");
                AddLine();

                AeroNode lastNode     = null;
                int      sizeCombined = 0;
                CreateLogicFlow(cd, 
                    preNode: node =>
                    {
                        if (node is AeroArrayNode arrayNode) {
                            //CreateForFromNode(arrayNode);
                        }
                    },
                    onNode: node =>
                    {
                        var wasNonFixedSize = false;
                        if (node is AeroFieldNode fieldNode) {
                            if (TypeHandlers.TryGetValue(fieldNode.TypeStr, out AeroTypeHandler handler)) {
                                if (!node.Parent.IsRoot && node.Parent is AeroArrayNode farrayNode && farrayNode.Mode == AeroArrayNode.Modes.Fixed) {
                                    sizeCombined += handler.Size * farrayNode.Length;
                                }
                                else {
                                    sizeCombined += handler.Size;
                                }
                            }
                            else {
                                AddLine($"// {fieldNode.GetFullName()} had unknown type {fieldNode.TypeStr}");
                            }
                        }
                        else if (node is AeroStringNode stringNode) {
                            AddLine($"offset += {stringNode.GetFullName()}.Length; // string");
                            wasNonFixedSize = true;
                        }
                        else if (node is AeroArrayNode arrayNode && arrayNode.Mode != AeroArrayNode.Modes.Fixed) {
                            AddLine($"offset += {arrayNode.GetFullName()}.Length; // array non fixed");
                            wasNonFixedSize = true;
                        }

                        if (wasNonFixedSize) {
                            AddLine($"offset += {sizeCombined}; // combined size");
                            sizeCombined = 0;
                        }

                        lastNode = node;
                    });

                if (sizeCombined > 0) {
                    AddLine($"offset += {sizeCombined};");
                }
                
                AddLine("return offset;");
            }
        }*/

        private void CreateStringReader(AeroStringNode stringNode, AeroNode node)
        {
            var readStringCall = "Encoding.UTF8.GetString";
            var lenName        = $"{stringNode.Name}Len";

            switch (stringNode.Mode) {
                case AeroStringNode.Modes.Ref:
                    AddLines($"{node.GetFullName()} = {readStringCall}(data.Slice(offset, {stringNode.RefFieldName}));",
                        $"offset += {stringNode.RefFieldName};");
                    break;
                case AeroStringNode.Modes.LenTypePrefixed:
                    if (TypeHandlers.TryGetValue(stringNode.PrefixTypeStr.ToLower(), out AeroTypeHandler handler)) {
                        var sizeOfKey = handler.Size;
                        AddLines($"{stringNode.PrefixTypeStr} {handler.Reader(lenName, null)}",
                            $"offset += {sizeOfKey};",
                            "",
                            $"{node.GetFullName()} = {readStringCall}(data.Slice(offset, {lenName}));",
                            $"offset += {lenName};");
                    }

                    break;
                case AeroStringNode.Modes.Fixed:
                    AddLines($"{node.GetFullName()} = {readStringCall}(data.Slice(offset, {stringNode.Length}));",
                        $"offset += {stringNode.Length};");
                    break;
                case AeroStringNode.Modes.NullTerminated:
                    var reachedEndName = $"{stringNode.Name}{stringNode.Depth}_ReachedEndOfSpan";
                    AddLines($"int {lenName} = data.Slice(offset, data.Length - offset).IndexOf<byte>(0x00);",
                        $"bool {reachedEndName} = {lenName} == -1;",
                        $"{lenName} = {reachedEndName} ? (data.Length - offset) : {lenName};",
                        $"{node.GetFullName()} = {readStringCall}(data.Slice(offset, {lenName}));",
                        $"offset += {reachedEndName} ? {lenName} : ({lenName} + 1);");
                    break;
            }
        }

        private void CreateForFromNode(AeroArrayNode arrayNode, bool createArray = true)
        {
            var idxName      = $"idx{arrayNode.Depth}";
            var firstSubNode = arrayNode.Nodes.FirstOrDefault(x => x is AeroFieldNode or AeroBlockNode or AeroStringNode);

            switch (arrayNode.Mode) {
                case AeroArrayNode.Modes.Ref:
                    if (createArray) {
                        AddLine($"{firstSubNode.GetFullName(true)} = new {firstSubNode.TypeStr}[{arrayNode.RefFieldName}];");
                    }

                    AddLine($"for (int {idxName} = 0; {idxName} < {arrayNode.RefFieldName}; {idxName}++)");
                    break;
                case AeroArrayNode.Modes.LenTypePrefixed:
                    var prefixName = $"array{arrayNode.Length}Len";
                    if (TypeHandlers.TryGetValue(arrayNode.PrefixTypeStr, out AeroTypeHandler handler)) {
                        AddLine($"var {handler.Reader(prefixName, null)}");
                        AddLine($"offset += {handler.Size};");
                        AddLine();

                        if (createArray) {
                            AddLine($"{firstSubNode.GetFullName(true)} = new {firstSubNode.TypeStr}[{prefixName}];");
                        }
                        AddLine($"for (int {idxName} = 0; {idxName} < {prefixName}; {idxName}++)");
                    }
                    else {
                        AddLine($"// Oh shit something went wrong and I couldn't read a type of {arrayNode.PrefixTypeStr} :<");
                    }

                    break;
                case AeroArrayNode.Modes.Fixed:
                    if (createArray) {
                        AddLine($"{firstSubNode.GetFullName(true)} = new {firstSubNode.TypeStr}[{arrayNode.Length}];");
                    }
                    AddLine($"for (int {idxName} = 0; {idxName} < {arrayNode.Length}; {idxName}++)");
                    break;
            }
        }

        // Boiler plate code for creating the logic flow
        private void CreateLogicFlow(ClassDeclarationSyntax cd, Action<AeroNode> preNode = null, Action<AeroNode> onNode = null, Action<AeroNode> postNode = null)
        {
            var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);

            var lastDepth = 0;
            var idx       = 0;
            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                if (node.IsRoot) return;
                
                if (lastDepth > node.Depth) {
                    for (int i = 0; i < lastDepth - node.Depth; i++) {
                        EndScope();
                        AddLine();
                    }
                }

                if (lastDepth < node.Depth) StartScope();
                
                if (node is AeroArrayNode aan) {
                    AddLine($"// Array {aan.Mode}");
                }
                
                preNode?.Invoke(node);
                
                if (node is AeroIfNode ain) {
                    AddLine($"if ({ain.Statement})");
                }

                if (node is AeroBlockNode abn) {
                    AddLine($"// Block {abn.Name}, Type: {abn.TypeStr}");
                }

                if (node is AeroFieldNode afn) {
                    AddLine($"// Field: {afn.Name}, Type: {afn.TypeStr}, enum type: {afn.EnumStr}");
                }

                onNode?.Invoke(node);
                postNode?.Invoke(node);

                lastDepth = node.Depth;
                idx++;
            });
            
            for (int i = 0; i < lastDepth; i++) {
                EndScope();
            }
        }
        
        public virtual void CreateReader(ClassDeclarationSyntax cd)
        {
            using (Function("public int Unpack(ReadOnlySpan<byte> data)")) {
                AddLine("int offset = 0;");
                AddLine();

                var  aeroFieldEnumerator = new AeroFieldEnumerator(cd, Context);
                int  lastDepth           = 0;
                bool closeScope          = false;
                foreach (var fieldInfo in aeroFieldEnumerator) {
                    CreateReader(fieldInfo, ref lastDepth, ref closeScope);
                }

                for (int i = 0; i < lastDepth; i++) {
                    EndScope(true);
                }

                AddLine("return offset;");
            }
        }

        public virtual void CreateGetPackedSize(ClassDeclarationSyntax cd)
        {
            using (Function("public int GetPackedSize()")) {
                AddLine("int size = 0;");
                AddLine();

                ClassFieldGen(cd, (fieldInfo, arrayStart) =>
                {
                    string sizeStr = "";

                    if (fieldInfo.IsString) {
                        if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.FixedSize) {
                            sizeStr = $"{fieldInfo.StringInfo.Length}";
                        }
                        else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.NullTerminated) {
                            sizeStr = $"({fieldInfo.FieldName}.Length + 1)";
                        }
                        else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                            sizeStr = $"sizeof({fieldInfo.StringInfo.KeyType}) + {fieldInfo.FieldName}.Length";
                        }
                        else {
                            sizeStr = $"{fieldInfo.FieldName}.Length";
                        }
                    }
                    else if (!fieldInfo.IsBlock) {
                        sizeStr = $"{GetTypeSize(fieldInfo.TypeStr)}";
                    }

                    if (fieldInfo.IsArray && !fieldInfo.IsBlock) {
                        if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.RefField) {
                            AddLine($"size += {sizeStr} * {fieldInfo.ArrayInfo.KeyName};");
                        }
                        else if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                            AddLine(
                                $"size += {GetTypeSize(fieldInfo.ArrayInfo.KeyType)} + ({sizeStr} * {fieldInfo.FieldName}.Length);");
                        }
                        else if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.FixedSize) {
                            AddLine($"size += {sizeStr} * {fieldInfo.ArrayInfo.Length};");
                        }
                    }
                    else if (arrayStart) {
                        //AddLine($"size += {GetTypeSize(fieldInfo.ArrayInfo.KeyType)} + ({sizeStr} * {fieldInfo.FieldName}.Length);");

                        var idxKey = $"idx{fieldInfo.Depth}";
                        AddLine($"for(int {idxKey} = 0; {idxKey} < {fieldInfo.FieldName}.Length; {idxKey}++)");
                        //using (ForLen("int", arrayLen, idxKey)) {
                    }
                    else {
                        AddLine($"size += {sizeStr};");
                    }
                });

                AddLine("return size;");
            }
        }

        public virtual void CreateWriter(ClassDeclarationSyntax cd)
        {
            using (Function("public int Pack(Span<byte> buffer)")) {
                AddLine("int offset = 0;");
                AddLine();

                ClassFieldGen(cd, (fieldInfo, arrayStart) =>
                {
                    // Skip over arrays for now
                    //if (!fieldInfo.IsArray) {
                    if (fieldInfo.IsString) {
                        var strName = fieldInfo.FieldName;

                        if (fieldInfo.IsArray) {
                            var idxKey = $"idx{fieldInfo.Depth}";
                            strName = $"{fieldInfo.FieldName}[{idxKey}]";
                            AddLine($"for(int {idxKey} = 0; {idxKey} < {fieldInfo.FieldName}.Length; {idxKey}++)");
                            StartScope();
                        }

                        if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.FixedSize ||
                            fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.RefField  ||
                            fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.NullTerminated) {
                            AddLines(
                                $"var {fieldInfo.FieldName}Bytes = Encoding.ASCII.GetBytes({strName}).AsSpan();",
                                $"{fieldInfo.FieldName}Bytes.CopyTo(buffer.Slice(offset, {fieldInfo.FieldName}Bytes.Length));");

                            if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.NullTerminated) {
                                AddLines($"buffer[offset + {fieldInfo.FieldName}Bytes.Length] = 0;",
                                    $"offset += ({fieldInfo.FieldName}Bytes.Length + 1);");
                            }
                            else {
                                AddLine($"offset += {fieldInfo.FieldName}Bytes.Length;");
                            }
                        }
                        else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                            CreateWriteType($"{strName}.Length", fieldInfo.StringInfo.KeyType,
                                fieldInfo.StringInfo.KeyType);
                            AddLines(
                                $"var {fieldInfo.FieldName}Bytes = Encoding.ASCII.GetBytes({strName}).AsSpan();",
                                $"{fieldInfo.FieldName}Bytes.CopyTo(buffer.Slice(offset, {fieldInfo.FieldName}Bytes.Length));",
                                $"offset += {fieldInfo.FieldName}Bytes.Length;");
                        }

                        if (fieldInfo.IsArray) {
                            EndScope();
                            AddLine();
                        }
                    }
                    else if (arrayStart) {
                        var idxKey = $"idx{fieldInfo.Depth}";
                        AddLine($"for(int {idxKey} = 0; {idxKey} < {fieldInfo.FieldName}.Length; {idxKey}++)");
                    }
                    else if (!fieldInfo.IsBlock) {
                        if (fieldInfo.IsArray) {
                            if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                                CreateWriteType($"({fieldInfo.ArrayInfo.KeyType}){fieldInfo.FieldName}.Length",
                                    fieldInfo.ArrayInfo.KeyType);
                            }

                            AddLine(
                                $"for(int idx{fieldInfo.Depth} = 0; idx{fieldInfo.Depth} < {fieldInfo.FieldName}.Length; idx{fieldInfo.Depth}++)");
                            StartScope();
                        }

                        CreateWriteType(
                            fieldInfo.IsArray ? $"{fieldInfo.FieldName}[idx{fieldInfo.Depth}]" : fieldInfo.FieldName,
                            fieldInfo.TypeStr, fieldInfo.IsEnum ? fieldInfo.EnumType : null);

                        if (fieldInfo.IsArray) {
                            EndScope();
                        }
                    }
                    //}
                });

                AddLine("return offset;");
            }
        }

        public virtual void ClassFieldGen(ClassDeclarationSyntax cd, Action<AeroFieldInfo, bool> OnField)
        {
            int lastDepth           = 0;
            var aeroFieldEnumerator = new AeroFieldEnumerator(cd, Context);
            foreach (var fieldInfoItem in aeroFieldEnumerator) {
                HandleField(fieldInfoItem);
            }

            for (int i = 0; i < lastDepth; i++) {
                EndScope();
            }

            void HandleField(AeroFieldInfo fieldInfo)
            {
                AddLine(
                    $"// {fieldInfo.FieldName}, Type: {fieldInfo.TypeStr}, IsArray: {fieldInfo.IsArray}, IsBlock: {fieldInfo.IsBlock}, Depth: {fieldInfo.Depth}");
                bool hasIf = fieldInfo.IfStatment != null;

                if (hasIf) {
                    AddLine($"if ({fieldInfo.IfStatment}) ");
                }

                if (fieldInfo.Depth > lastDepth || (hasIf && !fieldInfo.IsBlock)) {
                    StartScope();
                }
                else if (fieldInfo.Depth < lastDepth) {
                    for (int i = 0; i < lastDepth - fieldInfo.Depth; i++) EndScope();
                }

                lastDepth = fieldInfo.Depth;

                if (fieldInfo.IsArray && fieldInfo.IsBlock) {
                    OnField(fieldInfo, true);

                    var subFields = fieldInfo.GetSubFieldsForArrayBlock(SyntaxReceiver,
                        $"{fieldInfo.FieldName}[idx{fieldInfo.Depth}]");
                    foreach (var subField in subFields) {
                        //subField.IsArray   = true;
                        subField.ArrayInfo = fieldInfo.ArrayInfo;
                        subField.Depth++;
                        HandleField(subField);
                    }
                }
                else if (!fieldInfo.IsBlock) {
                    OnField(fieldInfo, false);
                }

                if (hasIf && !fieldInfo.IsBlock) {
                    EndScope();
                }
            }
        }

        private void CreateReader(AeroFieldInfo fieldInfo, ref int lastDepth, ref bool closeScope)
        {
            bool hasIf = fieldInfo.IfStatment != null;
            if (hasIf) AddLine($"if ({fieldInfo.IfStatment})");

            {
                var depthDiff = fieldInfo.Depth - lastDepth;
                if (depthDiff < 0) {
                    for (int i = 0; i < -depthDiff; i++) {
                        EndScope(true);
                    }
                }

                //if (closeScope) EndScope();
                if (fieldInfo.Depth          > lastDepth || (hasIf && !fieldInfo.IsBlock)) StartScope();
                closeScope = fieldInfo.Depth < lastDepth;
                lastDepth  = fieldInfo.Depth;
            }

            if (fieldInfo.IsArray) {
                CreateArrayReader(fieldInfo, ref lastDepth, ref closeScope);
            }
            else if (fieldInfo.IsString) {
                CreateStringReader(fieldInfo, ref lastDepth, ref closeScope);
            }
            else if (!fieldInfo.IsArray && !fieldInfo.IsBlock) {
                AddLine($"// Read {fieldInfo.FieldName}, type: {fieldInfo.TypeStr}");
                var typeStr = fieldInfo.IsEnum ? fieldInfo.EnumType : fieldInfo.TypeStr;
                using (AddBoundsCheck(fieldInfo.FieldName, typeStr)) {
                    CreateReadType(fieldInfo.FieldName, typeStr, fieldInfo.IsEnum ? fieldInfo.TypeStr : null);
                }
            }

            if (hasIf && !fieldInfo.IsBlock) {
                EndScope();
            }
        }

        private void CreateArrayReader(AeroFieldInfo fieldInfo, ref int lastDepth, ref bool closeScope)
        {
            string arrayLen = "";
            if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.FixedSize) {
                arrayLen = $"{fieldInfo.ArrayInfo.Length}";
                AddLine($"{fieldInfo.FieldName} = new {fieldInfo.TypeStr}[{arrayLen}];");
            }
            else if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                arrayLen = $"{fieldInfo.FieldName}Len";
                using (AddBoundsCheck(arrayLen, fieldInfo.TypeStr)) {
                    AddLine(
                        $"{fieldInfo.ArrayInfo.KeyType} {arrayLen} = MemoryMarshal.Read<{fieldInfo.ArrayInfo.KeyType}>(data.Slice(offset, sizeof({fieldInfo.ArrayInfo.KeyType})));");
                }

                AddLine($"offset += sizeof({fieldInfo.ArrayInfo.KeyType});");
                AddLine($"{fieldInfo.FieldName} = new {fieldInfo.TypeStr}[{arrayLen}];");
            }
            else if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.RefField) {
                arrayLen = $"{fieldInfo.ArrayInfo.KeyName}";
                AddLine($"{fieldInfo.FieldName} = new {fieldInfo.TypeStr}[{arrayLen}];");
            }

            var idxKey = $"idx{fieldInfo.Depth}";
            using (ForLen("int", arrayLen, idxKey)) {
                if (fieldInfo.IsBlock) {
                    foreach (var fieldInfo2 in fieldInfo.GetSubFieldsForArrayBlock(SyntaxReceiver,
                        $"{fieldInfo.FieldName}[{idxKey}]")) {
                        CreateReader(fieldInfo2, ref lastDepth, ref closeScope);
                    }
                }
                else {
                    var arrFInfo = new AeroFieldInfo
                    {
                        FieldName  = $"{fieldInfo.FieldName}[{idxKey}]",
                        TypeStr    = fieldInfo.TypeStr,
                        IsArray    = false,
                        IsBlock    = false,
                        IsString   = fieldInfo.IsString,
                        ArrayInfo  = fieldInfo.ArrayInfo,
                        StringInfo = fieldInfo.StringInfo
                    };
                    CreateReader(arrFInfo, ref lastDepth, ref closeScope);
                }
            }
        }

        private void CreateStringReader(AeroFieldInfo fieldInfo, ref int lastDepth, ref bool closeScope)
        {
            var nonIdxName = fieldInfo.FieldName.IndexOf("[") > 0
                ? fieldInfo.FieldName.Substring(0, fieldInfo.FieldName.IndexOf("["))
                : fieldInfo.FieldName;
            nonIdxName = $"{nonIdxName}Str";
            if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.FixedSize) {
                using (AddBoundsCheckKnownLength(fieldInfo.FieldName, fieldInfo.TypeStr,
                    $"{fieldInfo.StringInfo.Length}")) {
                    AddLine(
                        $"{fieldInfo.FieldName} = Encoding.UTF8.GetString(data.Slice(offset, {fieldInfo.StringInfo.Length}));");
                    AddLine($"offset += {fieldInfo.StringInfo.Length};");
                }
            }
            else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.RefField) {
                var arrayLen = $"{fieldInfo.StringInfo.KeyName}";
                using (AddBoundsCheckKnownLength(fieldInfo.FieldName, fieldInfo.TypeStr,
                    $"{fieldInfo.StringInfo.Length}")) {
                    AddLine($"{fieldInfo.FieldName} = Encoding.UTF8.GetString(data.Slice(offset, {arrayLen}));");
                    AddLine($"offset += {arrayLen};");
                }
            }
            else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.LengthType) {
                string arrayLen;

                using (AddBoundsCheck(fieldInfo.FieldName, fieldInfo.StringInfo.KeyType)) {
                    arrayLen = $"{nonIdxName}Len";
                    AddLine(
                        $"{fieldInfo.StringInfo.KeyType} {arrayLen} = MemoryMarshal.Read<{fieldInfo.StringInfo.KeyType}>(data.Slice(offset, sizeof({fieldInfo.StringInfo.KeyType})));");
                    AddLine($"offset += sizeof({fieldInfo.StringInfo.KeyType});");
                }

                using (AddBoundsCheckKnownLength(fieldInfo.FieldName, fieldInfo.TypeStr, $"{arrayLen}")) {
                    AddLine($"{fieldInfo.FieldName} = Encoding.UTF8.GetString(data.Slice(offset, {arrayLen}));");
                    AddLine($"offset += {arrayLen};");
                }
            }
            else if (fieldInfo.StringInfo.ArrayMode == AeroArrayInfo.Mode.NullTerminated) {
                // Read until a 0x00 or the end of the span
                // Get the length to read
                var lenName        = $"{nonIdxName}Len";
                var reachedEndName = $"{nonIdxName}ReachedEndOfSpan";
                AddLine($"int {lenName} = data.Slice(offset, data.Length - offset).IndexOf<byte>(0x00);");
                AddLine($"bool {reachedEndName} = {lenName} == -1;");
                AddLine($"{lenName} = {reachedEndName} ? (data.Length - offset) : {lenName};");
                AddLine($"{fieldInfo.FieldName} = Encoding.UTF8.GetString(data.Slice(offset, {lenName}));");
                AddLine($"offset += {reachedEndName} ? {lenName} : ({lenName} + 1);");
            }
        }

        public virtual void AddUsings()
        {
            foreach (var use in UsingsToAdd) {
                AddLine($"using {use};");
            }
        }

        public virtual void CreateReadType(string name, string typeStr, string castType = null)
        {
            bool   wasHandled = true;
            string typeCast   = castType != null ? $"({castType})" : "";

            switch (typeStr) {
                case "byte":
                    AddLine($"{name} = {typeCast}data[offset];");
                    break;
                case "char":
                    AddLine($"{name} = ({typeCast}(char)data[offset]);");
                    typeStr = "byte";
                    break;
                case "int":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "uint":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "short":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "ushort":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "double":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "float":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "ulong":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "long":
                    AddLine(
                        $"{name} = {typeCast}BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;

                case "Vector2":
                    AddLines($"{name} = new Vector2{{",
                        "X = MemoryMarshal.Read<float>(data.Slice(offset, 4)),",
                        "Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4))",
                        "};");
                    AddLine($"offset += 8;"); // 2 floats
                    wasHandled = false;
                    break;
                case "Vector3":
                    AddLines($"{name} = new Vector3{{",
                        "X = MemoryMarshal.Read<float>(data.Slice(offset, 4)),",
                        "Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4)),",
                        "Z = MemoryMarshal.Read<float>(data.Slice(offset + 8, 4))",
                        "};");
                    AddLine($"offset += 12;"); // 3 floats
                    wasHandled = false;
                    break;
                case "Vector4":
                    AddLines($"{name} = new Vector4{{",
                        "X = MemoryMarshal.Read<float>(data.Slice(offset, 4)),",
                        "Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4)),",
                        "Z = MemoryMarshal.Read<float>(data.Slice(offset + 8, 4)),",
                        "W = MemoryMarshal.Read<float>(data.Slice(offset + 12, 4))",
                        "};");
                    AddLine($"offset += 16;"); // 4 floats
                    wasHandled = false;
                    break;
                case "Quaternion":
                    AddLines($"{name} = new Quaternion{{",
                        "X = MemoryMarshal.Read<float>(data.Slice(offset, 4)),",
                        "Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4)),",
                        "Z = MemoryMarshal.Read<float>(data.Slice(offset + 8, 4)),",
                        "W = MemoryMarshal.Read<float>(data.Slice(offset + 12, 4))",
                        "};");
                    AddLine($"offset += 16;"); // 4 floats
                    wasHandled = false;
                    break;

                default:
                    AddLine($"// Unhandled type {typeStr}");
                    wasHandled = false;
                    break;
            }

            if (wasHandled) {
                AddLine($"offset += sizeof({typeStr});");
            }

            AddLine();
        }

        public virtual void CreateWriteType(string name, string typeStr, string castType = null)
        {
            bool   wasHandled = true;
            string typeCast   = castType != null ? $"({castType})" : "";

            switch (typeStr) {
                case "byte":
                    AddLine($"buffer[offset] = {typeCast}{name};");
                    break;
                case "char":
                    AddLine($"buffer[offset] = {typeCast}((byte){name});");
                    typeStr = "byte";
                    break;
                case "int":
                    AddLine(
                        $"BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "uint":
                    AddLine(
                        $"BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "short":
                    AddLine(
                        $"BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "ushort":
                    AddLine(
                        $"BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "double":
                    AddLine(
                        $"BinaryPrimitives.WriteDoubleLittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "float":
                    AddLine(
                        $"BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "ulong":
                    AddLine(
                        $"BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;
                case "long":
                    AddLine(
                        $"BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset, sizeof({typeStr})), {name});");
                    break;

                case "Vector2":
                    AddLines(
                        $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);",
                        "offset += 8;");
                    wasHandled = false;
                    break;
                case "Vector3":
                    AddLines(
                        $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 8, sizeof(float)), ref {name}.Z);",
                        "offset += 12;");
                    wasHandled = false;
                    break;
                case "Vector4":
                case "Quaternion":
                    AddLines(
                        $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 4, sizeof(float)), ref {name}.Y);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 8, sizeof(float)), ref {name}.Z);",
                        $"MemoryMarshal.Write(buffer.Slice(offset + 12, sizeof(float)), ref {name}.W);",
                        "offset += 16;");
                    wasHandled = false;
                    break;

                default:
                    AddLine($"// Unhandled type {typeStr}");
                    wasHandled = false;
                    break;
            }

            if (wasHandled) {
                AddLine($"offset += sizeof({typeStr});");
            }

            AddLine();
        }
    }
}