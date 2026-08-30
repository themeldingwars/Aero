using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
using Aero.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class AeroSyntaxReceiver : ISyntaxReceiver
    {
        public GeneratorExecutionContext    Context;
        public List<ClassDeclarationSyntax> ClassesToAugment { get; private set; } = new();

        public List<ClassDeclarationSyntax>                AeroClasses          { get; private set; } = new();
        public List<ClassDeclarationSyntax>                AeroEncounterClasses { get; private set; } = new();
        public Dictionary<string, StructDeclarationSyntax> AeroBlockLookup      { get; private set; } = new();

        // AeroMessageId attributes, resolved (semantic model) during generator Execute
        public List<MessageIdRef>                        MessageIdRefs { get; private set; } = new();
        public Dictionary<string, AeroMessageIdAttribute> AeroMessageIds { get; private set; } = new();

        public class MessageIdRef
        {
            public SyntaxTree              Tree;
            public AttributeSyntax         Attribute;
            public ClassDeclarationSyntax  Class;
        }

        public static bool HasAttribute(ClassDeclarationSyntax cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => (y.Name is IdentifierNameSyntax ins && ins.Identifier.Text == attributeName) ||
                                                                                                                                               (y.Name is QualifiedNameSyntax qns && qns.ToString() == attributeName)));
        public static bool HasAttribute(TypeDeclarationSyntax  cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => (y.Name is IdentifierNameSyntax ins && ins.Identifier.Text == attributeName) ||
                                                                                                                                               (y.Name is QualifiedNameSyntax qns && qns.ToString() == attributeName)));

        // Get all classes with the Aero attribute
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0 && HasAttribute(cds, "Aero")) {
                ClassesToAugment.Add(cds);
                AeroClasses.Add(cds);

                var attributeInfos = AgUtils.NodesWithName<AttributeSyntax>(cds, AeroMessageIdAttribute.Name).ToList();
                if (attributeInfos.Any()) {
                    foreach (var attributeInfo in attributeInfos) {
                        MessageIdRefs.Add(new MessageIdRef
                        {
                            Tree      = cds.SyntaxTree,
                            Attribute = attributeInfo,
                            Class     = cds,
                        });
                    }
                }

                if (HasAttribute(cds, "AeroEncounter"))
                {
                    AeroEncounterClasses.Add(cds);
                }
            }

            if (syntaxNode is StructDeclarationSyntax sds && sds.AttributeLists.Count > 0 && HasAttribute(sds, AeroBlockAttribute.Name)) {
                AeroBlockLookup.Add(sds.GetFullName(), sds);
            }
        }

        // Resolve the collected AeroMessageId attributes via the semantic model, filling AeroMessageIds
        public void ResolveMessageIds(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            foreach (var mref in MessageIdRefs) {
                var semanticModel = compilation.GetSemanticModel(mref.Tree);
                var (ok, info, error) = AgUtils.GetAeroMessageIdAttributeInfo(mref.Attribute, semanticModel);
                if (!ok) {
                    context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.InvalidMessageIdError, mref.Attribute.GetLocation(), error));
                    continue;
                }

                if (!NormaliseVersionRange(info, compilation, mref.Attribute.GetLocation(), context))
                    continue;

                if (!CheckViewPresent(info, context, mref.Attribute.GetLocation()))
                    continue;

                info.FullClassName = mref.Class.GetFullName();
                var asString = info.GetAsString();

                if (AeroMessageIds.ContainsKey(asString)) {
                    context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.MultipleMessageIdsForTheSameType, mref.Class.GetLocation(), asString));
                }
                else {
                    AeroMessageIds.Add(asString, info);
                }
            }

            CheckVersionRangeOverlaps(context);
        }

        // A VersionTo of -1 means "through the last known version of that family"
        private bool NormaliseVersionRange(AeroMessageIdAttribute info, Compilation compilation, Location location, GeneratorExecutionContext context)
        {
            if (info.VersionFrom < 0) {
                context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.InvalidMessageIdError, location, $"version range start must be a valid {info.VersionEnum} member"));
                return false;
            }

            if (info.VersionTo < 0) {
                int versionCount = info.VersionEnum switch
                {
                    "MatrixVersion" => MatrixTables.VersionCount,
                    "GssVersion"    => GssTables.VersionCount,
                    _               => 0
                };

                if (versionCount == 0) {
                    context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.InvalidMessageIdError, location, $"version enum '{info.VersionEnum}' is not a known Aero.Protocol version enum"));
                    return false;
                }

                info.VersionTo = versionCount - 1;
            }

            if (info.VersionFrom > info.VersionTo) {
                context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.InvalidMessageIdError, location, $"version range V{info.VersionFrom + 1}..V{info.VersionTo + 1} is inverted"));
                return false;
            }

            return true;
        }

        // A specified view must exist in every version of the range, otherwise there is no route to
        // send the message through in those versions.
        private bool CheckViewPresent(AeroMessageIdAttribute info, GeneratorExecutionContext context, Location location)
        {
            if (info.ViewOrdinal < 0)
                return true;

            if (!GssTables.TryGetProtocolEnumInfo(info.MessageEnum, out int ns, out _))
                return true;

            var missing = new List<string>();
            for (int v = info.VersionFrom; v <= info.VersionTo; v++)
            {
                if (GssTables.GetMessageId((GssVersion)v, ns, GssTables.Kind.View, info.ViewOrdinal) == 0)
                    missing.Add($"V{v + 1}");
            }

            if (missing.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.InvalidMessageIdError, location,
                    $"{info.ViewEnum}.{info.ViewName} does not exist in {string.Join(", ", missing)}, so {info.MessageEnum}.{info.MessageName} cannot be routed through it in those versions"));
                return false;
            }

            return true;
        }

        private void CheckVersionRangeOverlaps(GeneratorExecutionContext context)
        {
            var groups = AeroMessageIds.Values
                .Where(x => !x.IsControl)
                .GroupBy(x => (x.Typ, x.MessageEnum, x.MessageOrdinal, x.ViewOrdinal));

            foreach (var group in groups) {
                var list = group.ToList();
                for (int i = 0; i < list.Count; i++) {
                    for (int j = i + 1; j < list.Count; j++) {
                        var a = list[i];
                        var b = list[j];

                        bool srcOverlap   = a.Src == b.Src || a.Src == AeroMessageIdAttribute.MsgSrc.Both || b.Src == AeroMessageIdAttribute.MsgSrc.Both;
                        bool rangeOverlap = a.VersionFrom <= b.VersionTo && b.VersionFrom <= a.VersionTo;

                        if (srcOverlap && rangeOverlap) {
                            context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.VersionRangeOverlapError, Location.None,
                                $"{a.MessageEnum}.{a.MessageOrdinal}", a.FullClassName,
                                $"{b.MessageEnum}.{b.MessageOrdinal}", b.FullClassName,
                                $"V{a.VersionFrom + 1}..V{a.VersionTo + 1}", $"V{b.VersionFrom + 1}..V{b.VersionTo + 1}"));
                        }
                    }
                }
            }
        }

        public StructDeclarationSyntax GetAeroBLockOfName(string ns, string name)
        {
            if (AeroBlockLookup.TryGetValue(name, out StructDeclarationSyntax sds)) {
                return sds;
            }

            return null;
        }
    }

    public static class StructDeclarationSyntaxExtensions
    {
        public const string NESTED_CLASS_DELIMITER    = "+";
        public const string NAMESPACE_CLASS_DELIMITER = ".";

        public static string GetFullName(this StructDeclarationSyntax source)
        {
            var items  = new List<string>();
            var parent = source.Parent;
            while (parent.IsKind(SyntaxKind.StructDeclaration))
            {
                var parentClass = parent as StructDeclarationSyntax;
                items.Add(parentClass.Identifier.Text);

                parent = parent.Parent;
            }

            var nameSpace = parent as NamespaceDeclarationSyntax;
            var sb        = new StringBuilder().Append(nameSpace.Name).Append(NAMESPACE_CLASS_DELIMITER);
            items.Reverse();
            items.ForEach(i => { sb.Append(i).Append(NESTED_CLASS_DELIMITER); });
            sb.Append(source.Identifier.Text);

            var result = sb.ToString();
            return result;
        }
    }

    public static class ClassDeclarationSyntaxExtensions
    {
        public const string NESTED_CLASS_DELIMITER    = "+";
        public const string NAMESPACE_CLASS_DELIMITER = ".";

        public static string GetFullName(this ClassDeclarationSyntax source)
        {
            var items  = new List<string>();
            var parent = source.Parent;
            while (parent.IsKind(SyntaxKind.ClassDeclaration))
            {
                var parentClass = parent as ClassDeclarationSyntax;
                items.Add(parentClass.Identifier.Text);

                parent = parent.Parent;
            }

            var nameSpace = parent as NamespaceDeclarationSyntax;
            var sb        = new StringBuilder().Append(nameSpace.Name).Append(NAMESPACE_CLASS_DELIMITER);
            items.Reverse();
            items.ForEach(i => { sb.Append(i).Append(NESTED_CLASS_DELIMITER); });
            sb.Append(source.Identifier.Text);

            var result = sb.ToString();
            return result;
        }
    }
}