using System;
using System.Collections.Generic;
using System.Linq;
using Aero.Gen;
using Aero.Gen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Aero.Gen.Attributes.AeroIfAttribute;

namespace Aero.Gen
{
    public struct AeroIfInfo
    {
        public string   Key;
        public Ops      Op;
        public string[] Values;

        public string GetIfStr(string nameBase = "")
        {
            var key = nameBase != "" ? $"{nameBase}.{Key}" : Key;

            bool isFlagsCheck = Op is Ops.HasFlag or Ops.DoesntHaveFlag;
            var opStr = Op switch
            {
                Ops.Equal          => "==",
                Ops.NotEqual       => "!=",
                Ops.HasFlag        => "&",
                Ops.DoesntHaveFlag => "&",
                _                  => ""
            };

            var op    = Op;
            //var inner = Values.Select(x => isFlagsCheck ? $"({key} {opStr} {x}) {(op == Ops.HasFlag ? "!=" : "==")} 0" : $"{key} {opStr} {x}");
            var inner = Values.Select(x => isFlagsCheck ? $"({key} {opStr} {x}) {(op == Ops.HasFlag ? "!=" : "==")} 0" : $"{key} {opStr} {x}");
            return $"({string.Join(" || ", inner)})";
        }
    }

    public struct AeroArrayInfo
    {
        public enum Mode : byte
        {
            RefField,
            LengthType,
            FixedSize,
            NullTerminated
        }

        public Mode   ArrayMode;
        public bool   IsArray;
        public string KeyName;
        public int    Length;
        public string KeyType;
    }

    public static class AgUtils
    {
        public static string GetClassName(ClassDeclarationSyntax cd) => cd.Identifier.Text;
        public static string GetNamespace(ClassDeclarationSyntax cd) => cd.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.ToString();

        // Get all the fields on this class that we should serialise
        public static IEnumerable<FieldDeclarationSyntax> GetClassFields(ClassDeclarationSyntax cd)
        {
            var fields = cd.Members.OfType<FieldDeclarationSyntax>()
                           .Where(x => x.DescendantTokens()
                                        .Any(y => y.Kind() == SyntaxKind.PublicKeyword));

            return fields;
        }

        public static IEnumerable<FieldDeclarationSyntax> GetStructFields(StructDeclarationSyntax sd)
        {
            var fields = sd.Members.OfType<FieldDeclarationSyntax>()
                           .Where(x => x.DescendantTokens()
                                        .Any(y => y.Kind() == SyntaxKind.PublicKeyword));

            return fields;
        }

        public static string GetFieldName(FieldDeclarationSyntax fd) => fd.Declaration.Variables.First().Identifier.Text;

        public static string GetFieldTypeStr(FieldDeclarationSyntax fd)
        {
            if (fd.Declaration.Type is PredefinedTypeSyntax pdt) {
                return pdt.Keyword.Text;
            }

            if (fd.Declaration.Type is ArrayTypeSyntax at) {
                return at.ToString();
            }

            return fd.Declaration.Type.ToString();
        }

        // Get a node with the given name
        public static T NodeWithName<T>(SyntaxNode root, string name) where T : SyntaxNode =>
            root.DescendantNodes().FirstOrDefault(x => x is T node &&
                                                       node.DescendantNodes().OfType<IdentifierNameSyntax>()
                                                           .First().Identifier.Text == name) as T;

        // Get nodes with the given name
        public static IEnumerable<T> NodesWithName<T>(SyntaxNode root, string name) where T : SyntaxNode =>
            root.DescendantNodes().Where(x => x is T node &&
                                              node.DescendantNodes().OfType<IdentifierNameSyntax>()
                                                  .First().Identifier.Text == name).Select(x => (T) x);


        public static AttributeSyntax GetAttributeByName(FieldDeclarationSyntax fd, string name) => NodeWithName<AttributeSyntax>(fd, name);

        // Get the value of a nameof or the string if it doesn't have a nameof used
        public static string GetFieldRefName(ExpressionSyntax expression)
        {
            if (expression is InvocationExpressionSyntax ies
             && ies.ArgumentList.Arguments.Single().Expression is IdentifierNameSyntax ins) {
                return ins.ToString().Trim('"');
            }

            return expression.ToString().Trim('"');
        }

        public static List<AeroIfInfo> GetAeroIfAttributes(FieldDeclarationSyntax fd, SemanticModel semanticModel)
        {
            var ifInfos = new List<AeroIfInfo>();

            var ifAttrs = NodesWithName<AttributeSyntax>(fd, Name);
            foreach (var attr in ifAttrs) {
                if (attr.ArgumentList?.Arguments.Count >= 2) {
                    var keyData   = attr.ArgumentList.Arguments[0].Expression;
                    Ops op        = Ops.Equal;
                    var ifStrings = new List<string>();

                    foreach (var arg in attr.ArgumentList.Arguments.Skip(1)) {
                        var argStr  = arg.Expression.ToString();
                        var argType = semanticModel.GetTypeInfo(arg);

                        // Op
                        if (argStr.StartsWith("AeroIfAttribute.Ops") || argStr.StartsWith("Ops")) {
                            var opStr = argStr.Replace("AeroIfAttribute.Ops.", "").Replace("Ops.", "");
                            if (Enum.TryParse<Ops>(opStr, out Ops parsedOp)) {
                                op = parsedOp;
                            }
                        }
                        else {
                            ifStrings.Add(arg.Expression.ToString());
                        }
                    }

                    var info = new AeroIfInfo
                    {
                        Key    = GetFieldRefName(keyData),
                        Op     = op,
                        Values = ifStrings.ToArray()
                    };

                    ifInfos.Add(info);
                }
            }

            return ifInfos;
        }

        public static AeroArrayInfo GetArrayInfo(FieldDeclarationSyntax fd)
        {
            var data = new AeroArrayInfo
            {
                IsArray = true
            };

            var arrayAttr = NodeWithName<AttributeSyntax>(fd, AeroArrayAttribute.Name);
            if (arrayAttr == null) return new AeroArrayInfo {IsArray = false};

            var numArgs = arrayAttr.ArgumentList?.Arguments.Count ?? 0;
            if (numArgs == 1) {
                var args = arrayAttr.ArgumentList.Arguments.ToArray();
                data.IsArray = true;

                if (args[0].Expression is InvocationExpressionSyntax ies && ies.ArgumentList.Arguments.Single().Expression is IdentifierNameSyntax ins) {
                    data.ArrayMode = AeroArrayInfo.Mode.RefField;
                    data.KeyName   = ins.ToString();
                }

                if (args[0].Expression is LiteralExpressionSyntax le) {
                    data.ArrayMode = AeroArrayInfo.Mode.FixedSize;
                    data.Length    = int.Parse(le.ToString());
                }

                if (args[0].Expression is TypeOfExpressionSyntax es && es.Type is PredefinedTypeSyntax pdt) {
                    data.ArrayMode = AeroArrayInfo.Mode.LengthType;
                    data.KeyType   = pdt.ToString();
                }
            }

            return data;
        }
        
        public static AeroArrayInfo GetStringInfo(FieldDeclarationSyntax fd)
        {
            var data = new AeroArrayInfo
            {
                IsArray = true
            };

            var arrayAttr = NodeWithName<AttributeSyntax>(fd, AeroStringAttribute.Name);
            if (arrayAttr == null) return new AeroArrayInfo {IsArray = false};

            var numArgs = arrayAttr.ArgumentList?.Arguments.Count ?? 0;
            if (numArgs == 1) {
                var args = arrayAttr.ArgumentList.Arguments.ToArray();
                data.IsArray = true;

                if (args[0].Expression is InvocationExpressionSyntax ies && ies.ArgumentList.Arguments.Single().Expression is IdentifierNameSyntax ins) {
                    data.ArrayMode = AeroArrayInfo.Mode.RefField;
                    data.KeyName   = ins.ToString();
                }

                if (args[0].Expression is LiteralExpressionSyntax le) {
                    data.ArrayMode = AeroArrayInfo.Mode.FixedSize;
                    data.Length    = int.Parse(le.ToString());
                }

                if (args[0].Expression is TypeOfExpressionSyntax es && es.Type is PredefinedTypeSyntax pdt) {
                    data.ArrayMode = AeroArrayInfo.Mode.LengthType;
                    data.KeyType   = pdt.ToString();
                }
            }
            else if (numArgs == 0) {
                data.IsArray   = true;
                data.ArrayMode = AeroArrayInfo.Mode.NullTerminated;
            }

            return data;
        }

        /*{
            return NodeWithName<AttributeSyntax>(fd, name);
                fd.DescendantNodes().OfType<AttributeSyntax>()
                     .First(x => x.DescendantNodes().OfType<IdentifierNameSyntax>().Any(y => y.Identifier.Text == name));
                     
                     .FirstOrDefault(x => Enumerable.OfType<IdentifierNameSyntax>(x.DescendantNodes()).Any(y =>
                y.Identifier.Text == name));
        }*/
    }
}

/*
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
*/