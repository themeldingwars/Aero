using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
        }

    #region Code adding functions

        protected static int  TabSpaces = 4;
        public           void Indent()                        => IndentLevel += TabSpaces;
        public           void UnIndent()                      => IndentLevel -= TabSpaces;
        public           void AddLine(string line)            => Sb.AppendLine($"{new string(' ', IndentLevel)}{line}");
        public           void AddLine()                       => AddLine("");
        public           void AddLines(params string[] lines) => Array.ForEach(lines, AddLine);

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
            
            var ifStatment = @$"if (data.Length < (offset + sizeof({sizeOfTypeStr})))";
            return new AgBlock(this, () =>
            {
                if (Config.BoundsCheck) {
                    if (Config.DiagLogging) {
                        AddLine($"{ifStatment} {{");
                        Indent();
                        {
                            AddLines(
                                @$"LogDiag($""Failed to read {typeStr}({{sizeof({sizeOfTypeStr})}} bytes) for {fieldName}, offset: {{offset}} data length: {{data.Length}}, read overflowed by {{(offset + sizeof({sizeOfTypeStr})) - data.Length}}."");",
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
                    AddDiagLog($"Read {typeStr}({{sizeof({sizeOfTypeStr})}} bytes) for {fieldName} at offset {{offset - sizeof({sizeOfTypeStr})}}.");
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

        private void CreateReader(AeroFieldInfo fieldInfo, ref int lastDepth, ref bool closeScope)
        {
            bool hasIf = fieldInfo.IfStatment != null;
            if (hasIf) AddLine($"if ({fieldInfo.IfStatment})");

            {
                if (closeScope) EndScope();
                if (fieldInfo.Depth          > lastDepth || hasIf) StartScope();
                closeScope = fieldInfo.Depth < lastDepth;
                lastDepth  = fieldInfo.Depth;
            }

            if (!fieldInfo.IsArray && !fieldInfo.IsBlock) {
                AddLine($"// Read {fieldInfo.FieldName}, type: {fieldInfo.TypeStr}");
                var typeStr = fieldInfo.IsEnum ? fieldInfo.EnumType : fieldInfo.TypeStr;
                using (AddBoundsCheck(fieldInfo.FieldName, typeStr)) {
                    CreateReadType(fieldInfo.FieldName, typeStr, fieldInfo.IsEnum ? fieldInfo.TypeStr : null);
                }
            }
            else if (fieldInfo.IsArray) {
                CreateArrayReader(fieldInfo, ref lastDepth, ref closeScope);
            }

            if (hasIf) {
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
                    AddLine($"int {arrayLen} = MemoryMarshal.Read<{fieldInfo.TypeStr}>(data.Slice(offset, sizeof({fieldInfo.TypeStr})));");
                }

                AddLine($"offset += sizeof(int);");
                AddLine($"{fieldInfo.FieldName} = new {fieldInfo.TypeStr}[{arrayLen}];");
            }
            else if (fieldInfo.ArrayInfo.ArrayMode == AeroArrayInfo.Mode.RefField) {
                arrayLen = $"{fieldInfo.ArrayInfo.KeyName}";
                AddLine($"{fieldInfo.FieldName} = new {fieldInfo.TypeStr}[{arrayLen}];");
            }

            var idxKey = $"idx{fieldInfo.Depth}";
            using (ForLen("int", arrayLen, idxKey)) {
                if (fieldInfo.IsBlock) {
                    foreach (var fieldInfo2 in fieldInfo.GetSubFieldsForArrayBlock(SyntaxReceiver,$"{fieldInfo.FieldName}[{idxKey}]")) {
                        CreateReader(fieldInfo2, ref lastDepth, ref closeScope);
                    }
                }
                else {
                    var arrFInfo = new AeroFieldInfo
                    {
                        FieldName = $"{fieldInfo.FieldName}[{idxKey}]",
                        TypeStr   = fieldInfo.TypeStr,
                        IsArray   = false,
                        IsBlock   = false
                    };
                    CreateReader(arrFInfo, ref lastDepth, ref closeScope);
                }
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
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "uint":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "short":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "ushort":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "double":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "float":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "ulong":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, sizeof({typeStr})));");
                    break;
                case "long":
                    AddLine($"{name} = {typeCast}BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, sizeof({typeStr})));");
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