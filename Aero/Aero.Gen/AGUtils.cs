using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Aero.Gen;
using Aero.Gen.Attributes;
using Aero.Protocol;
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

            var op = Op;
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

    public struct AeroBlobInfo
    {
        public enum Mode : byte
        {
            ReadToEnd,
            LengthType,
            RefField
        }

        public Mode   BlobMode;
        public bool   IsBlob;
        public string KeyName;
        public string KeyType;
        public string Error;
    }

    public static class AgUtils
    {
        public static string GetClassName(ClassDeclarationSyntax cd) => cd.Identifier.Text;
        public static string GetNamespace(ClassDeclarationSyntax cd) => cd.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.ToString();

        // Get all the fields on this class that we should serialise
        public static IEnumerable<FieldDeclarationSyntax> GetClassFields(ClassDeclarationSyntax cd, bool allowPrivate = false)
        {
            var fields = cd.Members.OfType<FieldDeclarationSyntax>()
                           .Where(x => x.DescendantTokens()
                                        .Any(y => y.Kind() == SyntaxKind.PublicKeyword || y.Kind() == SyntaxKind.PrivateKeyword && allowPrivate));

            return fields;
        }

        public static IEnumerable<FieldDeclarationSyntax> GetStructFields(StructDeclarationSyntax sd, bool allowPrivate = false)
        {
            var fields = sd.Members.OfType<FieldDeclarationSyntax>()
                           .Where(x => x.DescendantTokens()
                                        .Any(y => y.Kind() == SyntaxKind.PublicKeyword || y.Kind() == SyntaxKind.PrivateKeyword && allowPrivate));

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

        public static bool HasNodeWithName<T>(SyntaxNode root, string name) where T : SyntaxNode => NodesWithName<T>(root, name).FirstOrDefault() != default;


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

                if (args[0].Expression is PrefixUnaryExpressionSyntax pe) {
                    data.ArrayMode = AeroArrayInfo.Mode.NullTerminated;
                    data.Length    = int.Parse(pe.ToString());
                }

                if (args[0].Expression is TypeOfExpressionSyntax es && es.Type is PredefinedTypeSyntax pdt) {
                    data.ArrayMode = AeroArrayInfo.Mode.LengthType;
                    data.KeyType   = pdt.ToString();
                }
            }

            return data;
        }

        public static AeroBlobInfo GetBlobInfo(FieldDeclarationSyntax fd)
        {
            var data = new AeroBlobInfo
            {
                IsBlob = true
            };

            var blobAttr = NodeWithName<AttributeSyntax>(fd, AeroBlobAttribute.Name);
            if (blobAttr == null) return new AeroBlobInfo {IsBlob = false};

            var numArgs = blobAttr.ArgumentList?.Arguments.Count ?? 0;
            if (numArgs == 0) {
                data.BlobMode = AeroBlobInfo.Mode.ReadToEnd;
            }
            else if (numArgs == 1) {
                var arg = blobAttr.ArgumentList.Arguments[0].Expression;

                if (arg is InvocationExpressionSyntax ies && ies.ArgumentList.Arguments.Single().Expression is IdentifierNameSyntax ins) {
                    data.BlobMode = AeroBlobInfo.Mode.RefField;
                    data.KeyName  = ins.ToString();
                }
                else if (arg is LiteralExpressionSyntax le && le.IsKind(SyntaxKind.StringLiteralExpression)) {
                    data.BlobMode = AeroBlobInfo.Mode.RefField;
                    data.KeyName  = GetFieldRefName(arg);
                }
                else if (arg is TypeOfExpressionSyntax es && es.Type is PredefinedTypeSyntax pdt) {
                    data.BlobMode = AeroBlobInfo.Mode.LengthType;
                    data.KeyType  = pdt.ToString();
                }
                else if (arg is LiteralExpressionSyntax) {
                    data.Error = $"AeroBlob doesn't support a fixed length argument, use [AeroArray(n)] instead";
                }
                else {
                    data.Error = $"AeroBlob argument '{arg}' isn't supported, use no argument, typeof(lengthType) or nameof(field)";
                }
            }
            else {
                data.Error = $"AeroBlob supports at most one argument, got {numArgs}";
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

        public static (bool ok, AeroMessageIdAttribute info, string error) GetAeroMessageIdAttributeInfo(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
        {
            if (attributeSyntax.ArgumentList == null || attributeSyntax.ArgumentList.Arguments.Count < 3)
                return (false, null, "AeroMessageId requires at least 3 arguments");

            var args = attributeSyntax.ArgumentList.Arguments;
            if (args.Count > 6)
                return (false, null, "AeroMessageId supports at most 6 arguments");

            if (!TryGetEnumConstant(semanticModel, args[0], typeof(AeroMessageIdAttribute.MsgType), out var typValue, out var typError))
                return (false, null, typError);
            if (!TryGetEnumConstant(semanticModel, args[1], typeof(AeroMessageIdAttribute.MsgSrc), out var srcValue, out var srcError))
                return (false, null, srcError);

            var typ = (AeroMessageIdAttribute.MsgType)typValue;
            var src = (AeroMessageIdAttribute.MsgSrc)srcValue;
            var thirdType = semanticModel.GetTypeInfo(args[2].Expression).Type;

            if (thirdType != null && thirdType.SpecialType == SpecialType.System_Int32)
            {
                if (typ != AeroMessageIdAttribute.MsgType.Control)
                    return (false, null, $"numeric AeroMessageId ids can only be used for Control messages, got {typ}");

                var idConstant = semanticModel.GetConstantValue(args[2].Expression);
                if (!idConstant.HasValue)
                    return (false, null, "the numeric AeroMessageId id must be a constant");

                return (true, new AeroMessageIdAttribute(typ, src, Convert.ToInt32(idConstant.Value)), null);
            }

            if (thirdType is INamedTypeSymbol messageEnum && messageEnum.TypeKind == TypeKind.Enum)
            {
                var messageConstant = semanticModel.GetConstantValue(args[2].Expression);
                if (!messageConstant.HasValue)
                    return (false, null, $"the AeroMessageId message argument '{args[2].Expression}' must be a constant enum member");

                string messageEnumName = messageEnum.Name;
                string messageName = EnumMemberName(args[2].Expression);
                int messageOrdinal = Convert.ToInt32(messageConstant.Value);

                string versionEnumName = messageEnumName == "MatrixMessage" ? "MatrixVersion" : "GssVersion";
                if (messageEnumName == "MatrixMessage" && typ != AeroMessageIdAttribute.MsgType.Matrix)
                    return (false, null, $"MatrixMessage can only be used with MsgType.Matrix, got {typ}");
                if (messageEnumName != "MatrixMessage" && typ != AeroMessageIdAttribute.MsgType.GSS)
                    return (false, null, $"{messageEnumName} can only be used with MsgType.GSS, got {typ}");
                int enumKind = 0;
                if (messageEnumName != "MatrixMessage" && !GssTables.TryGetProtocolEnumInfo(messageEnumName, out _, out enumKind))
                    return (false, null, $"'{messageEnumName}' is not a supported AeroMessageId protocol enum");

                if (messageEnumName != "MatrixMessage")
                {
                    if (enumKind == GssTables.Kind.View)
                    {
                        if (src == AeroMessageIdAttribute.MsgSrc.Command)
                            return (false, null, $"{messageEnumName} is a view and cannot be used with MsgSrc.Command");
                    }
                    else
                    {
                        if (messageEnumName == "GssMessage" && src == AeroMessageIdAttribute.MsgSrc.Command)
                            return (false, null, "GssMessage is a server -> client message and cannot be used with MsgSrc.Command");
                        if (messageEnumName.EndsWith("Command") && src == AeroMessageIdAttribute.MsgSrc.Message)
                            return (false, null, $"{messageEnumName} is a client -> command and cannot be used with MsgSrc.Message");
                        if (messageEnumName.EndsWith("Message") && src == AeroMessageIdAttribute.MsgSrc.Command)
                            return (false, null, $"{messageEnumName} is a server -> client message and cannot be used with MsgSrc.Command");
                    }
                }

                string viewEnumName = null;
                int viewOrdinal = -1;
                string viewName = null;
                int versionArgIndex = 3;

                if (messageEnumName != "MatrixMessage" && args.Count > 3
                    && semanticModel.GetTypeInfo(args[3].Expression).Type is INamedTypeSymbol thirdArgEnum
                    && thirdArgEnum.TypeKind == TypeKind.Enum
                    && thirdArgEnum.Name.EndsWith("View", StringComparison.Ordinal))
                {
                    if (enumKind == GssTables.Kind.View)
                        return (false, null, $"{messageEnumName} is a view class and cannot take a view argument, use (typ, src, {messageEnumName}, from, to)");

                    string expectedViewEnum = messageEnumName.EndsWith("Message", StringComparison.Ordinal) || messageEnumName.EndsWith("Command", StringComparison.Ordinal)
                        ? messageEnumName.Substring(0, messageEnumName.Length - 7) + "View"
                        : null;
                    if (expectedViewEnum == null)
                        return (false, null, $"{messageEnumName} has no views, the view argument is not allowed");
                    if (thirdArgEnum.Name != expectedViewEnum)
                        return (false, null, $"'{thirdArgEnum.Name}' is not a view of the namespace of '{messageEnumName}', expected '{expectedViewEnum}'");

                    var viewConstant = semanticModel.GetConstantValue(args[3].Expression);
                    if (!viewConstant.HasValue)
                        return (false, null, $"the AeroMessageId view argument '{args[3].Expression}' must be a constant enum member");

                    viewEnumName = thirdArgEnum.Name;
                    viewOrdinal = Convert.ToInt32(viewConstant.Value);
                    viewName = EnumMemberName(args[3].Expression);
                    versionArgIndex = 4;
                }

                int versionFrom = 0;
                int versionTo = -1;
                if (args.Count > versionArgIndex)
                {
                    if (!TryGetEnumConstant(semanticModel, args[versionArgIndex], null, out versionFrom, out var fromError))
                        return (false, null, fromError);
                }
                if (args.Count > versionArgIndex + 1)
                {
                    if (!TryGetEnumConstant(semanticModel, args[versionArgIndex + 1], null, out versionTo, out var toError))
                        return (false, null, toError);
                }

                return (true, AeroMessageIdAttribute.CreateProtocol(typ, src, messageEnumName, messageOrdinal, messageName, versionEnumName, versionFrom, versionTo, viewEnumName, viewOrdinal, viewName), null);
            }

            return (false, null, "the third AeroMessageId argument must be a numeric id for Control messages or a protocol message enum for Matrix/GSS messages");
        }

        static string EnumMemberName(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => expression.ToString()
        };

        static bool TryGetEnumConstant(SemanticModel semanticModel, AttributeArgumentSyntax argument, Type expectedEnum, out int value, out string error)
        {
            var constant = semanticModel.GetConstantValue(argument.Expression);
            if (!constant.HasValue)
            {
                value = 0;
                error = $"the AeroMessageId argument '{argument.Expression}' must be a constant enum value";
                return false;
            }

            var argumentType = semanticModel.GetTypeInfo(argument.Expression).Type;
            if (expectedEnum != null && argumentType != null && !argumentType.Name.Equals(expectedEnum.Name, StringComparison.Ordinal))
            {
                value = 0;
                error = $"the AeroMessageId argument '{argument.Expression}' must be {expectedEnum.Name}, got {argumentType.Name}";
                return false;
            }

            value = Convert.ToInt32(constant.Value);
            error = null;
            return true;
        }

        public static bool IsViewClass(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            var aeroAttr = NodeWithName<AttributeSyntax>(cd, AeroAttribute.Name);

            if (aeroAttr.ArgumentList is {Arguments: {Count: 1}}
             && (aeroAttr.ArgumentList.Arguments[0].Expression.ToFullString().EndsWith(nameof(AeroGenTypes.View))
             || aeroAttr.ArgumentList.Arguments[0].Expression.ToFullString().EndsWith(nameof(AeroGenTypes.Controller)))) {
                return true;
            }

            return false;
        }

        public static bool IsEncounterClass(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            var aeroAttr = NodeWithName<AttributeSyntax>(cd, AeroEncounterAttribute.Name);

            return aeroAttr != null;
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