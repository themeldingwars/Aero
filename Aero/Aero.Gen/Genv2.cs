using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private string GetTypeSize(string typeStr)
        {
            var sizeOfTypeStr = typeStr == "char" ? "byte" : typeStr;
            var size = typeStr switch
            {
                "Vector2"    => "8",
                "Vector3"    => "12",
                "Vector4"    => "16",
                "Quaternion" => "16",
                _            => $"sizeof({sizeOfTypeStr})"
            };

            return size;
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

                    CreateReader(cd);
                    AddLine();

                    CreateGetPackedSize(cd);
                    AddLine();

                    CreateWriter(cd);
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

        public virtual void AddDiagLog(string msg) => AddLine(@$"LogDiag($""{msg}"");");

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
                                    CreateWriteType($"({fieldInfo.ArrayInfo.KeyType}){fieldInfo.FieldName}.Length", fieldInfo.ArrayInfo.KeyType);
                                }
                                
                                AddLine($"for(int idx{fieldInfo.Depth} = 0; idx{fieldInfo.Depth} < {fieldInfo.FieldName}.Length; idx{fieldInfo.Depth}++)");
                                StartScope();
                            }
                            
                            CreateWriteType(fieldInfo.IsArray ? $"{fieldInfo.FieldName}[idx{fieldInfo.Depth}]" : fieldInfo.FieldName, fieldInfo.TypeStr, fieldInfo.IsEnum ? fieldInfo.EnumType : null);

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