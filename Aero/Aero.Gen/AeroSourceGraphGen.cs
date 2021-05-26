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
        public string         Name = null;
        public string         TypeStr;
        public int            Depth;
        public bool           IsRoot;
        public AeroNode       Parent;
        public List<AeroNode> Nodes = new();

        // Get the full name of this node in the list, eg test.test2.int;
        public string GetFullName(bool ingoreLastArray = false)
        {
            var names = new List<string>(4);
            ClimbUp(this, true);
            names.Reverse();

            var fullName = string.Join('.', names);
            return fullName;

            void ClimbUp(AeroNode node, bool isFirstNode)
            {
                if (node != null && !node.IsRoot && node is not AeroIfNode) {
                    if (node?.Parent is AeroBlockNode blockNode && blockNode?.Parent is AeroArrayNode arrayNode) {
                        names.Add($"{blockNode.Name}[idx{arrayNode.Depth}]{node.Name}");
                        ClimbUp(arrayNode, false);
                        return;
                    }
                    else if (isFirstNode && node is AeroArrayNode arrayNode3) {
                        names.Add($"{arrayNode3.Nodes.First().Name}");
                        ClimbUp(node.Parent, false);
                        return;
                    }
                    else if (node.Name != null) {
                        if ((!ingoreLastArray && isFirstNode) && node?.Parent is AeroArrayNode arrayNode2) {
                            names.Add($"{node.Name}[idx{arrayNode2.Depth}]");
                        }
                        else {
                            names.Add(node.Name);
                        }
                    }

                    ClimbUp(node.Parent, false);
                }
            }
        }

        public virtual bool IsFixedSize() => false;

        public virtual int GetSize() => -1;
    }

    public class AeroFieldNode : AeroNode
    {
        public string EnumStr;
        public bool   IsEnum;
        public bool   IsFlags;

        public override bool IsFixedSize() => true;
        public override int  GetSize()     => Genv2.GetTypeSize(TypeStr);
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
        
        public override bool IsFixedSize() => GetSize() != -1;
        
        public override int GetSize()
        {
            int combinedSize = 0;
            foreach (var node in Nodes) {
                if (node.GetSize() == -1) {
                    return -1;
                }
                combinedSize += node.GetSize();
            }

            return combinedSize;
        }
    }

    public class AeroIfNode : AeroNode
    {
        public string           Statement;
        public List<AeroIfInfo> IfInfos;

        public override bool IsFixedSize() => false;

        public virtual int GetSize() => -1;
    }

    public class AeroBlockNode : AeroNode
    {
        public override bool IsFixedSize() => GetSize() != -1;
        
        public override int GetSize()
        {
            int combinedSize = 0;
            foreach (var node in Nodes) {
                if (node.GetSize() == -1) {
                    return -1;
                }
                combinedSize += node.GetSize();
            }

            return combinedSize;
        }
    }

    public class AeroStringNode : AeroNode
    {
        public enum Modes : byte
        {
            Ref,
            LenTypePrefixed,
            Fixed,
            NullTerminated
        }

        public Modes  Mode;
        public int    Length;
        public string RefFieldName;
        public string PrefixTypeStr;

        public override bool IsFixedSize() => Mode == Modes.Fixed;

        public override int GetSize()
        {
            if (Mode == Modes.Fixed) {
                return Length;
            }

            return -1;
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
                IsRoot = parent == null,
                Depth  = parent?.Depth ?? -1,
                Parent = parent
            };
            AeroNode currentNode = rootNode;

            foreach (var field in fields) {
                var fieldName = AgUtils.GetFieldName(field);
                var sModel    = snr.Context.Compilation.GetSemanticModel(field.SyntaxTree);
                var typeInfo  = sModel.GetTypeInfo(field.Declaration.Type).Type;
                var typeStr   = typeInfo?.ToString();

                // Ifs
                var ifAttrs = AgUtils.GetAeroIfAttributes(field, sModel);
                if (ifAttrs.Count > 0) {
                    var ifNode = new AeroIfNode
                    {
                        Statement = string.Join(" && ", ifAttrs.Select(x => x.GetIfStr())),
                        IfInfos   = ifAttrs.ToList(),
                        Parent    = currentNode,
                        Depth     = currentNode.Depth + 1
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
                        Parent        = currentNode,
                        Depth         = currentNode.Depth + 1
                    };

                    currentNode.Nodes.Add(arrayNode);
                    currentNode = arrayNode;
                }

                // Blocks
                var fieldType = currentNode is AeroArrayNode
                    ? typeStr.TrimEnd(new[] {'[', ']'})
                    : typeStr;
                var aeroBlock      = snr.GetAeroBLockOfName(null, fieldType);
                var stringAttrData = AgUtils.GetStringInfo(field);

                if (aeroBlock != null) {
                    var aeroBlockNode = new AeroBlockNode
                    {
                        Parent  = currentNode,
                        Name    = fieldName,
                        TypeStr = fieldType,
                        Depth   = currentNode.Depth + 1
                    };

                    foreach (var structField in AgUtils.GetStructFields(aeroBlock)) {
                        var subField = BuildTree(snr, new[] {structField}, aeroBlockNode);
                        aeroBlockNode.Nodes.Add(subField);
                    }

                    currentNode.Nodes.Add(aeroBlockNode);
                    currentNode = rootNode;
                }
                else if (stringAttrData.IsArray) {
                    var stringNode = new AeroStringNode
                    {
                        Name          = fieldName,
                        TypeStr       = "string",
                        Mode          = ((AeroStringNode.Modes) (int) stringAttrData.ArrayMode),
                        Length        = stringAttrData.Length,
                        RefFieldName  = stringAttrData.KeyName,
                        PrefixTypeStr = stringAttrData.KeyType,
                        Parent        = currentNode,
                        Depth         = currentNode.Depth + 1
                    };

                    currentNode.Nodes.Add(stringNode);
                    currentNode = rootNode;
                }
                else {
                    // Kinda hacky :/ :<
                    if (currentNode?.Parent is AeroStringNode) {
                        currentNode.Parent.Name = fieldName;
                    }
                    else {
                        var fieldNode = new AeroFieldNode
                        {
                            Name = fieldName,
                            Parent = currentNode,
                            TypeStr = typeStr.TrimEnd(new[] {'[', ']'}),
                            EnumStr =
                                typeInfo is INamedTypeSymbol ns ? ns.EnumUnderlyingType?.Name.ToLower() ?? "" : "",
                            IsEnum  = typeInfo?.TypeKind == TypeKind.Enum,
                            IsFlags = typeInfo?.GetAttributes().Any(x => x.AttributeClass.Name == "Flags") == true,
                            Depth   = currentNode.Depth + 1
                        };

                        currentNode.Nodes.Add(fieldNode);
                        currentNode = rootNode;
                    }
                }
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
                    }
                    else if (arrayNode.Mode == AeroArrayNode.Modes.LenTypePrefixed) {
                        sb.Append($"Length prefix type: {arrayNode.PrefixTypeStr}");
                    }
                    else if (arrayNode.Mode == AeroArrayNode.Modes.Ref) {
                        sb.Append($"Length ref var name: {arrayNode.RefFieldName}");
                    }
                }
                else if (node is AeroIfNode ifNode) {
                    sb.Append($"❓ If, {ifNode.Statement}");
                }
                else if (node is AeroFieldNode afnode) {
                    sb.Append(
                        $"🖊️ Field, Name: {afnode.Name}, Type: {afnode.TypeStr}, IsEnum: {afnode.IsEnum}, IsFlags: {afnode.IsFlags},");
                }
                else if (node is AeroBlockNode blockNode) {
                    sb.Append($"📦 Block, Name: {blockNode.Name}, Type: {blockNode.TypeStr}");
                }
                else if (node is AeroStringNode stringNode) {
                    sb.Append($"✍️ String, Name: {stringNode.Name}, Mode: {stringNode.Mode}");
                }
                else {
                    sb.Append($"Unknown, {node}");
                }

                sb.AppendLine($" Depth: {node.Depth}");

                level++;
                foreach (var subNode in node.Nodes) {
                    AppendNode(subNode);
                }

                level--;
            }
        }

        public static void WalkTree(AeroNode rootNode, Action<AeroNode> visitNode)
        {
            OnNode(rootNode);

            void OnNode(AeroNode node)
            {
                visitNode(node);

                foreach (var subNode in node.Nodes) {
                    OnNode(subNode);
                }
            }
        }
    }
}