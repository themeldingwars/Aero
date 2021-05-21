using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class AeroNode
    {
        public int            Depth;
        public bool           IsRoot;
        public AeroNode       Parent;
        public List<AeroNode> Nodes = new();
    }

    public class AeroFieldNode : AeroNode
    {
        public string Name;
        public string TypeStr;
        public string EnumStr;
        public bool   IsEnum;
        public bool   IsFlags;
    }

    public class AeroArrayNode : AeroNode
    {
        public enum Modes : byte
        {
            Ref,
            LenTypePrefixed,
            Fixed
        }

        public Modes  Mode;
        public int    Length;
        public string RefFieldName;
        public string PrefixTypeStr;
    }

    public class AeroIfNode : AeroNode
    {
        public string           Statement;
        public List<AeroIfInfo> IfInfos;
    }

    public class AeroBlockNode : AeroNode
    {
        public string Name;
        public string TypeStr;
    }

    public class AeroStringNode : AeroFieldNode
    {
        public enum Modes : byte
        {
            Fixed,
            Ref,
            LenTypePrefixed,
            NullTerminated
        }
    }

    public static class AeroSourceGraphGen
    {
        public static AeroNode BuildTree(AeroSyntaxReceiver snr, ClassDeclarationSyntax cls) =>
            BuildTree(snr, AgUtils.GetClassFields(cls));

        public static AeroNode BuildTree(AeroSyntaxReceiver snr, StructDeclarationSyntax sls) =>
            BuildTree(snr, AgUtils.GetStructFields(sls));

        public static AeroNode BuildTree(AeroSyntaxReceiver snr, IEnumerable<FieldDeclarationSyntax> fields,
                                         AeroNode           parent = null)
        {
            var rootNode = new AeroNode
            {
                IsRoot = parent == null
            };
            AeroNode currentNode = rootNode;

            foreach (var field in fields) {
                var fieldName = AgUtils.GetFieldName(field);
                var sModel    = snr.Context.Compilation.GetSemanticModel(field.SyntaxTree);
                var typeInfo  = sModel.GetTypeInfo(field.Declaration.Type).Type;
                var typeStr   = AgUtils.GetFieldTypeStr(field);

                // Ifs6
                var ifAttrs = AgUtils.GetAeroIfAttributes(field, sModel);
                if (ifAttrs.Count > 0) {
                    var ifNode = new AeroIfNode
                    {
                        Statement = string.Join(" && ", ifAttrs.Select(x => x.GetIfStr())),
                        IfInfos   = ifAttrs.ToList(),
                        Parent    = currentNode
                    };

                    currentNode.Nodes.Add(ifNode);
                    currentNode = ifNode;
                }

                // Arrays
                if (typeStr?.EndsWith("[]") ?? false) {
                    var arrayAttrData = AgUtils.GetArrayInfo(field);
                    var arrayNode = new AeroArrayNode
                    {
                        Mode          = ((AeroArrayNode.Modes) (int) arrayAttrData.ArrayMode),
                        Length        = arrayAttrData.Length,
                        RefFieldName  = arrayAttrData.KeyName,
                        PrefixTypeStr = arrayAttrData.KeyType,
                        Parent        = currentNode
                    };

                    currentNode.Nodes.Add(arrayNode);
                    currentNode = arrayNode;
                }

                // Blocks
                var fieldType = currentNode is AeroArrayNode ? typeStr : field.Declaration.Type.ToString();
                var aeroBlock = snr.GetAeroBLockOfName(null, fieldType);

                if (aeroBlock != null && currentNode is not AeroArrayNode) {
                    var aeroBlockNode = new AeroBlockNode
                    {
                        Parent  = currentNode,
                        Name    = fieldName,
                        TypeStr = typeStr
                    };

                    foreach (var structField in AgUtils.GetStructFields(aeroBlock)) {
                        var subField = BuildTree(snr, new[] {structField}, aeroBlockNode);
                        aeroBlockNode.Nodes.Add(subField);
                    }
                    
                    currentNode.Nodes.Add(aeroBlockNode);
                    currentNode = aeroBlockNode;
                }

                var fieldNode = new AeroFieldNode
                {
                    Name    = fieldName,
                    Parent  = currentNode,
                    TypeStr = typeStr.TrimEnd(new []{ '[', ']' }),
                    EnumStr = typeInfo is INamedTypeSymbol ns ? ns.EnumUnderlyingType?.Name.ToLower() ?? "" : "",
                    IsEnum  = typeInfo?.TypeKind == TypeKind.Enum,
                    IsFlags = typeInfo?.GetAttributes().Any(x => x.AttributeClass.Name == "Flags") == true
                };

                currentNode.Nodes.Add(fieldNode);
                currentNode = rootNode;
            }

            return parent == null ? rootNode : rootNode.Nodes.First();
        }

        public static string PrintTree(AeroNode rootNode)
        {
            var sb    = new StringBuilder();
            int level = 0;
            AppendNode(rootNode);

            return sb.ToString();
            
            void AppendNode(AeroNode node)
            {
                sb.Append(new string(' ', Math.Max(level * 4, 0)));

                if (node.IsRoot) {
                    sb.AppendLine("Root Node");
                }
                else if (node is AeroArrayNode arrayNode) {
                    sb.Append($"📚 Array, {arrayNode.Mode}");
                    if (arrayNode.Mode == AeroArrayNode.Modes.Fixed) {
                        sb.Append($"Len, {arrayNode.Length}");
                    } else if (arrayNode.Mode == AeroArrayNode.Modes.LenTypePrefixed) {
                        sb.Append($"Length prefix type: {arrayNode.PrefixTypeStr}");
                    }else if (arrayNode.Mode == AeroArrayNode.Modes.Ref) {
                        sb.Append($"Length ref var name: {arrayNode.RefFieldName}");
                    }

                    sb.AppendLine();
                }
                else if (node is AeroIfNode ifNode) {
                    sb.AppendLine($"❓ If, {ifNode.Statement}");
                }
                else if (node is AeroFieldNode afnode) {
                    sb.AppendLine($"🖊️ Field, Name: {afnode.Name}, Type: {afnode.TypeStr}, IsEnum: {afnode.IsEnum}, IsFlags: {afnode.IsFlags},");
                }
                else if (node is AeroBlockNode blockNode) {
                    sb.AppendLine($"📦 Block, Name: {blockNode.Name}, Type: {blockNode.TypeStr}");
                }
                else {
                    sb.AppendLine($"Unknown, {node}");
                }

                level++;
                foreach (var subNode in node.Nodes) {
                    AppendNode(subNode);
                }
                
                level--;
            }
        }
    }
}