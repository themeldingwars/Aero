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
            public Func<string, string, string> Writer;
        }

        public static Dictionary<string, AeroTypeHandler> TypeHandlers = new()
        {
            {
                "byte", new AeroTypeHandler
                {
                    Size   = 1,
                    Reader = (name, typeCast) => $"{name} = {typeCast}data[offset];",
                    Writer = (name, typeCast) => $"buffer[offset] = {typeCast}{name};",
                }
            },
            {
                "char", new AeroTypeHandler
                {
                    Size   = 1,
                    Reader = (name, typeCast) => $"{name} = ({typeCast}(char)data[offset]);",
                    Writer = (name, typeCast) => $"buffer[offset] = {typeCast}((byte){name});",
                }
            },
            {
                "int", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, 4), {typeCast}{name});"
                }
            },
            {
                "int32", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => TypeHandlers["int"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["int"].Writer(name, typeCast)
                }
            },
            {
                "uint", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), {typeCast}{name});"
                }
            },
            {
                "uint32", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => TypeHandlers["uint"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["uint"].Writer(name, typeCast)
                }
            },
            {
                "short", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, 2));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(offset, 2), {typeCast}{name});"
                }
            },
            {
                "int16", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => TypeHandlers["short"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["short"].Writer(name, typeCast)
                }
            },
            {
                "ushort", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), {typeCast}{name});"
                }
            },
            {
                "uint16", new AeroTypeHandler
                {
                    Size   = 2,
                    Reader = (name, typeCast) => TypeHandlers["ushort"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["ushort"].Writer(name, typeCast)
                }
            },
            {
                "long", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset, 8), {typeCast}{name});"
                }
            },
            {
                "int64", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => TypeHandlers["long"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["long"].Writer(name, typeCast)
                }
            },
            {
                "ulong", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset, 8), {typeCast}{name});"
                }
            },
            {
                "uint64", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => TypeHandlers["ulong"].Reader(name, typeCast),
                    Writer = (name, typeCast) => TypeHandlers["ulong"].Writer(name, typeCast)
                }
            },
            {
                "float", new AeroTypeHandler
                {
                    Size   = 4,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset, 4), {typeCast}{name});"
                }
            },
            {
                "double", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name} = {typeCast}BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, 8));",
                    Writer = (name, typeCast) => $"BinaryPrimitives.WriteDoubleLittleEndian(buffer.Slice(offset, 8), {typeCast}{name});"
                }
            },
            {
                "system.numerics.vector2", new AeroTypeHandler
                {
                    Size   = 8,
                    Reader = (name, typeCast) => $"{name}.X = MemoryMarshal.Read<float>(data.Slice(offset, 4));" +
                                                                         $"{name}.Y = MemoryMarshal.Read<float>(data.Slice(offset + 4, 4));",
                    Writer = (name, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
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
                    Writer = (name, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
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
                    Writer = (name, typeCast) => $"MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), ref {name}.X);" +
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
                    Writer = (name, typeCast) => TypeHandlers["system.numerics.vector4"].Writer(name, typeCast)
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
        
        public void AddWriter(string name, string typeStr, string castTypeStr = null)
        {
            if (TypeHandlers.TryGetValue(typeStr.ToLower(), out AeroTypeHandler handler)) {
                var castedTypeStr = castTypeStr != null ? $"({castTypeStr})" : "";
                AddLine($"{handler.Writer(name, castedTypeStr)}");
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

                    CreatePackerV2(cd);
                    AddLine();
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
        
        public virtual void AddUsings()
        {
            foreach (var use in UsingsToAdd) {
                AddLine($"using {use};");
            }
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

        public virtual void CreatePackerV2(ClassDeclarationSyntax cd)
        {
            using (Function("public int Pack(Span<byte> buffer)")) {
                AddLine("int offset = 0;");
                AddLine();
                
                CreateLogicFlow(cd, 
                    preNode: node =>
                    {
                        if (node is AeroArrayNode arrayNode) {
                            CreateForFromNode(arrayNode, false , true);
                        }
                    },
                    onNode: node =>
                    {
                        if (node is AeroFieldNode fieldNode) {
                            AddWriter(fieldNode.GetFullName(),
                                (fieldNode.IsEnum ? fieldNode.EnumStr : fieldNode.TypeStr).ToLower(),
                                fieldNode.IsEnum ? fieldNode.EnumStr : null);
                        }
                        else if (node is AeroStringNode stringNode) {
                            CreateStringWriter(stringNode, node);
                        }
                    });
                
                AddLine("return offset;");
            }
        }
        
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
        
        private void CreateStringWriter(AeroStringNode stringNode, AeroNode node)
        {
            var writeStringCall = "Encoding.UTF8.GetBytes";
            var lenName        = $"{stringNode.Name}Len";

            switch (stringNode.Mode) {
                case AeroStringNode.Modes.Ref:
                case AeroStringNode.Modes.Fixed:
                case AeroStringNode.Modes.NullTerminated:
                    AddLines(
                        $"var {stringNode.Name}Bytes = {writeStringCall}({stringNode.GetFullName()}).AsSpan();",
                        $"{stringNode.Name}Bytes.CopyTo(buffer.Slice(offset, {stringNode.Name}Bytes.Length));");

                    if (stringNode.Mode == AeroStringNode.Modes.NullTerminated) {
                        AddLines($"buffer[offset + {stringNode.Name}Bytes.Length] = 0;",
                            $"offset += ({stringNode.Name}Bytes.Length + 1);");
                    }
                    else {
                        AddLine($"offset += {stringNode.Name}Bytes.Length;");
                    }
                    break;
                case AeroStringNode.Modes.LenTypePrefixed:
                    if (TypeHandlers.TryGetValue(stringNode.PrefixTypeStr.ToLower(), out AeroTypeHandler handler)) {
                        var sizeOfKey = handler.Size;
                        AddWriter($"{stringNode.GetFullName()}.Length", stringNode.PrefixTypeStr, stringNode.PrefixTypeStr);
                        AddLines(
                            $"var {stringNode.Name}Bytes = {writeStringCall}({stringNode.GetFullName()}).AsSpan();",
                            $"{stringNode.Name}Bytes.CopyTo(buffer.Slice(offset, {stringNode.Name}Bytes.Length));",
                            $"offset += {stringNode.Name}Bytes.Length;");
                    }
                    break;
            }
        }

        private void CreateForFromNode(AeroArrayNode arrayNode, bool createArray = true, bool addWriteLenPrefix = false)
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
                    var prefixName = $"array{firstSubNode.Name}{arrayNode.Depth}Len";
                    if (TypeHandlers.TryGetValue(arrayNode.PrefixTypeStr, out AeroTypeHandler handler)) {
                        if (!addWriteLenPrefix) {
                            AddLine($"var {handler.Reader(prefixName, null)}");
                        }
                        else {
                            var lenName  = $"{arrayNode.GetFullName()}.Length";
                            var typeName = $"({arrayNode.PrefixTypeStr})";
                            AddLine($"{handler.Writer(lenName, typeName)}");
                        }

                        AddLine($"offset += {handler.Size};");
                        AddLine();

                        if (createArray) {
                            AddLine($"{firstSubNode.GetFullName(true)} = new {firstSubNode.TypeStr}[{prefixName}];");
                        }
                        AddLine($"for (int {idxName} = 0; {idxName} < {arrayNode.GetFullName()}.Length; {idxName}++)");
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
    }
}