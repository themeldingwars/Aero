using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
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
        public List<AeroNode> Nodes      = new();
        public int            Index      = 0;
        public bool           IsNullable = false;

        // Get the full name of this node in the list, eg test.test2.int;
        public string GetFullName(bool ingoreLastArray = false)
        {
            var names = new List<string>(4);
            ClimbUp(this, true);
            names.Reverse();

            var fullName = string.Join(".", names);
            //fullName = IsNullable ? $"{fullName}.Value" : fullName;
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
                }

                if (node != null && !node.IsRoot) {
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
        public override int  GetSize() => Genv2.GetTypeSize(this.IsEnum ? EnumStr : TypeStr);
    }

    public class AeroArrayNode : AeroNode
    {
        public enum Modes : byte
        {
            Ref,
            LenTypePrefixed,
            Fixed,
            ReadToEnd
        }

        public Modes  Mode;
        public int    Length;
        public string RefFieldName;
        public string PrefixTypeStr;

        public override bool IsFixedSize()
        {
            if (Nodes[0].IsFixedSize() && Mode == Modes.Fixed) {
                return true;
            }

            return false;
        }

        public override int GetSize()
        {
            if (Mode == Modes.Fixed && (Nodes[0] is AeroFieldNode || (Nodes[0].IsFixedSize()))) {
                return Nodes[0].GetSize() * Math.Abs(Length);
            }

            if (Nodes[0] is AeroFieldNode) {
                return Nodes[0].GetSize();
            }

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

        public override int GetSize() => -1;
    }

    public class AeroBlockNode : AeroNode
    {
        public override bool IsFixedSize() => GetSize() != -1;

        public override int GetSize()
        {
            int combinedSize = 0;
            foreach (var node in Nodes) {
                if (node.GetSize() == -1 || !node.IsFixedSize()) {
                    return -1;
                }

                combinedSize += node.GetSize();
            }

            return combinedSize;
        }
    }

    public class AeroBlobNode : AeroNode
    {
        public enum Modes : byte
        {
            ReadToEnd,
            LenTypePrefixed,
            Ref
        }

        public Modes  Mode;
        public string PrefixTypeStr;
        public string RefFieldName;

        public override bool IsFixedSize() => false;

        public override int GetSize() => -1;
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
        public class BuildTreeState
        {
            public Dictionary<string, string> CastedNameToType = new();
            public int                        Idx              = 0;

            // A read-to-end blob was seen, fields after it can't be read
            public bool                       BlobReadToEndSeen;

            // Names of fields seen so far, used to warn when a blob ref points at a later field
            public HashSet<string>            SeenFieldNames = new();
        }

        public static AeroNode BuildTree(AeroSyntaxReceiver snr, ClassDeclarationSyntax cls, bool allowPrivate = false) =>
            BuildTree(snr, AgUtils.GetClassFields(cls, AgUtils.IsViewClass(cls, snr.Context.Compilation.GetSemanticModel(cls.SyntaxTree))));

        public static AeroNode BuildTree(AeroSyntaxReceiver snr, StructDeclarationSyntax sls, bool allowPrivate = false) =>
            BuildTree(snr, AgUtils.GetStructFields(sls, allowPrivate));

        public static AeroNode BuildTree(AeroSyntaxReceiver snr,           IEnumerable<FieldDeclarationSyntax> fields,
                                         AeroNode           parent = null, BuildTreeState                      state = null)
        {
            var rootNode = new AeroNode
            {
                IsRoot     = parent == null,
                Depth      = parent?.Depth ?? -1,
                Parent     = parent,
                IsNullable = false
            };

            state ??= new BuildTreeState();
            AeroNode currentNode = rootNode;

            var fieldList = fields.ToList();

            // Warn about blob refs that point at fields declared after the blob, the ref would read as default.
            // Only done at the top level, block fields are walked one at a time so the order can't be seen
            if (parent == null) {
                for (int i = 0; i < fieldList.Count; i++) {
                    var blobData = AgUtils.GetBlobInfo(fieldList[i]);
                    if (blobData.IsBlob && blobData.BlobMode == AeroBlobInfo.Mode.RefField && blobData.KeyName != null &&
                        !fieldList.Take(i).Any(f => AgUtils.GetFieldName(f) == blobData.KeyName)) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.AeroBlobRefAfterBlobWarning,
                            fieldList[i].GetLocation(), AgUtils.GetFieldName(fieldList[i]), blobData.KeyName));
                    }
                }
            }

            foreach (var field in fieldList) {
                AeroGenerator.LastCheckedField = field;

                var fieldName = AgUtils.GetFieldName(field);
                var sModel    = snr.Context.Compilation.GetSemanticModel(field.SyntaxTree);
                var typeInfo  = sModel.GetTypeInfo(field.Declaration.Type).Type;
                var typeStr   = typeInfo?.ToString();

                if (AgUtils.HasNodeWithName<AttributeSyntax>(field, AeroNullable.Name)) {
                    currentNode.IsNullable = true;
                }
                else {
                    currentNode.IsNullable = false;
                }

                // Ifs
                var ifAttrs = AgUtils.GetAeroIfAttributes(field, sModel);
                if (ifAttrs.Count > 0) {
                    var ifNode = new AeroIfNode
                    {
                        Statement = string.Join(" && ", ifAttrs.Select(x =>
                        {
                            if (state.CastedNameToType.TryGetValue($"{currentNode.GetFullName()}.{x.Key}",
                                    out string typeName)) {
                                x.Values = x.Values.Select(y =>
                                {
                                    var splitVals = y.Split('.');
                                    if (splitVals.Length > 0) {
                                        return $"{typeName}.{splitVals.Last()}";
                                    }

                                    return y;
                                }).ToArray();
                            }

                            return x.GetIfStr(currentNode.GetFullName());
                        })),
                        IfInfos = ifAttrs.ToList(),
                        Parent  = currentNode,
                        Depth   = currentNode.Depth + 1,
                        Index   = state.Idx++
                    };

                    currentNode.Nodes.Add(ifNode);
                    currentNode = ifNode;
                }

                // Blobs
                var blobAttrData = AgUtils.GetBlobInfo(field);
                if (blobAttrData.IsBlob) {
                    if (blobAttrData.Error != null) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.AeroBlobInvalidArgumentError,
                            field.GetLocation(), fieldName, blobAttrData.Error));
                        break;
                    }

                    var isBlobByteArray = typeInfo is IArrayTypeSymbol blobArrayType &&
                                          blobArrayType.Rank == 1 &&
                                          blobArrayType.ElementType.SpecialType == SpecialType.System_Byte;
                    if (!isBlobByteArray) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.AeroBlobNotByteArrayError,
                            field.GetLocation(), fieldName, typeStr));
                        break;
                    }

                    if (state.BlobReadToEndSeen) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.AeroBlobPositionError,
                            field.GetLocation(), fieldName,
                            blobAttrData.BlobMode == AeroBlobInfo.Mode.ReadToEnd
                                ? "only one read-to-end [AeroBlob] is allowed per class"
                                : "fields can't follow a read-to-end [AeroBlob]"));
                        break;
                    }

                    if (blobAttrData.BlobMode == AeroBlobInfo.Mode.ReadToEnd) {
                        var insideArray = false;
                        for (var ancestor = currentNode; ancestor != null && !ancestor.IsRoot; ancestor = ancestor.Parent) {
                            if (ancestor is AeroArrayNode) {
                                insideArray = true;
                                break;
                            }
                        }

                        if (insideArray) {
                            snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.AeroBlobPositionError,
                                field.GetLocation(), fieldName, "a read-to-end [AeroBlob] can't be inside an array"));
                            break;
                        }

                        state.BlobReadToEndSeen = true;
                    }

                    var blobNode = new AeroBlobNode
                    {
                        Name          = fieldName,
                        TypeStr       = "byte[]",
                        Mode          = (AeroBlobNode.Modes) (int) blobAttrData.BlobMode,
                        RefFieldName  = blobAttrData.KeyName,
                        PrefixTypeStr = blobAttrData.KeyType,
                        Parent        = currentNode,
                        Depth         = currentNode.Depth + 1,
                        Index         = state.Idx++,
                        IsNullable    = currentNode.IsNullable
                    };

                    currentNode.Nodes.Add(blobNode);
                    currentNode = rootNode;
                    continue;
                }

                // Arrays
                if (typeStr?.EndsWith("[]") ?? false) {
                    var arrayAttrData = AgUtils.GetArrayInfo(field);

                    if (!arrayAttrData.IsArray) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.NoArrayAttributeError,
                            field.GetLocation(), fieldName));
                        break;
                    }

                    var arrayNode = new AeroArrayNode
                    {
                        Mode          = ((AeroArrayNode.Modes) (int) arrayAttrData.ArrayMode),
                        Length        = arrayAttrData.Length,
                        RefFieldName  = arrayAttrData.KeyName,
                        PrefixTypeStr = arrayAttrData.KeyType,
                        Parent        = currentNode,
                        Depth         = currentNode.Depth + 1,
                        Index         = state.Idx++,
                        TypeStr       = typeStr
                    };

                    currentNode.Nodes.Add(arrayNode);
                    currentNode = arrayNode;
                }

                // Blocks
                var fieldType = currentNode is AeroArrayNode
                    ? typeStr.TrimEnd(new[] {'[', ']'})
                    : typeStr;
                var baseTypeName   = fieldType.Contains("<") ? fieldType.Split('<')[0] : fieldType;
                var aeroBlock      = snr.GetAeroBLockOfName(null, baseTypeName);
                var stringAttrData = AgUtils.GetStringInfo(field);

                if (aeroBlock != null) {
                    var aeroBlockNode = new AeroBlockNode
                    {
                        Parent     = currentNode,
                        Name       = fieldName,
                        TypeStr    = fieldType,
                        Depth      = currentNode.Depth + 1,
                        Index      = state.Idx++,
                        IsNullable = currentNode.IsNullable
                    };

                    foreach (var structField in AgUtils.GetStructFields(aeroBlock)) {
                        var subField = BuildTree(snr, new[] {structField}, aeroBlockNode, state);
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
                        Depth         = currentNode.Depth + 1,
                        Index         = state.Idx++
                    };

                    currentNode.Nodes.Add(stringNode);
                    currentNode = rootNode;
                }
                else {
                    // Is a class
                    if (typeInfo.TypeKind == TypeKind.Class) {
                        snr.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.ClassNotAllowedInAeroError,
                            field.GetLocation(), fieldName, typeInfo.Name));
                        break;
                    }

                    // Is a struct and not marked as an AeroBlock or a special type
                    if (!AeroGenerator.SpecialCasesTypes.Contains(baseTypeName.ToLower()) && !currentNode.IsNullable) {
                        if ((typeInfo.TypeKind == TypeKind.Struct && typeInfo.SpecialType == SpecialType.None ||
                             (typeInfo is IArrayTypeSymbol ats && ats.ElementType.TypeKind == TypeKind.Struct &&
                              ats.ElementType.SpecialType                                  == SpecialType.None))) {
                            snr.Context.ReportDiagnostic(Diagnostic.Create(
                                AeroGenerator.StructNotMarkedAsAeroBlockError, field.GetLocation(), fieldName,
                                baseTypeName));
                            break;
                        }
                    }

                    // Kinda hacky :/ :<
                    if (currentNode?.Parent is AeroStringNode) {
                        currentNode.Parent.Name = fieldName;
                    }
                    else {
                        var fieldNode = new AeroFieldNode
                        {
                            Name    = fieldName,
                            Parent  = currentNode,
                            TypeStr = typeStr.TrimEnd(new[] {'[', ']'}),
                            EnumStr =
                                typeInfo is INamedTypeSymbol ns ? ns.EnumUnderlyingType?.Name.ToLower() ?? "" : "",
                            IsEnum     = typeInfo?.TypeKind                                                   == TypeKind.Enum,
                            IsFlags    = typeInfo?.GetAttributes().Any(x => x.AttributeClass.Name == "Flags") == true,
                            Depth      = currentNode.Depth + 1,
                            Index      = state.Idx++,
                            IsNullable = currentNode.IsNullable
                        };

                        var nodeFullName = fieldNode.GetFullName();
                        if (!state.CastedNameToType.ContainsKey(nodeFullName) && fieldNode.IsEnum) {
                            state.CastedNameToType.Add(nodeFullName, fieldNode.TypeStr);
                        }

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
                    sb.Append($"📚 Array, {arrayNode.Mode}, IsFixed: {arrayNode.IsFixedSize()}, Size: {arrayNode.GetSize()} ");
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
                else if (node is AeroBlobNode blobNode) {
                    sb.Append($"🧱 Blob, Name: {blobNode.Name}, Mode: {blobNode.Mode}");
                    if (blobNode.Mode == AeroBlobNode.Modes.LenTypePrefixed) {
                        sb.Append($" Length prefix type: {blobNode.PrefixTypeStr}");
                    }
                    else if (blobNode.Mode == AeroBlobNode.Modes.Ref) {
                        sb.Append($" Length ref var name: {blobNode.RefFieldName}");
                    }
                }
                else if (node is AeroFieldNode afnode) {
                    sb.Append(
                        $"🖊️ Field, Name: {afnode.Name}, Type: {afnode.TypeStr}, IsEnum: {afnode.IsEnum}, IsFlags: {afnode.IsFlags}, IsNullable: {afnode.IsNullable}, ");
                }
                else if (node is AeroBlockNode blockNode) {
                    sb.Append($"📦 Block, Name: {blockNode.Name}, Type: {blockNode.TypeStr}, IsNullable: {blockNode.IsNullable}, Size: {blockNode.GetSize()}");
                }
                else if (node is AeroStringNode stringNode) {
                    sb.Append($"✍️ String, Name: {stringNode.Name}, Mode: {stringNode.Mode}, IsNullable: {stringNode.IsNullable}");
                }
                else {
                    sb.Append($"Unknown, {node}");
                }

                sb.AppendLine($" Index: {node.Index}, Depth: {node.Depth}");

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