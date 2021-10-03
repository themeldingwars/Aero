using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class AeroSyntaxReceiver : ISyntaxReceiver
    {
        public GeneratorExecutionContext    Context;
        public List<ClassDeclarationSyntax> ClassesToAugment { get; private set; } = new();
        
        public List<ClassDeclarationSyntax>                AeroClasses     { get; private set; } = new();
        public Dictionary<string, StructDeclarationSyntax> AeroBlockLookup { get; private set; } = new();
        public Dictionary<string, AeroMessageIdAttribute>  AeroMessageIds  { get; private set; } = new();

        public static bool HasAttribute(ClassDeclarationSyntax cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => (y.Name is IdentifierNameSyntax ins && ins.Identifier.Text == attributeName) ||
                                                                                                                                               (y.Name is QualifiedNameSyntax qns && qns.ToString() == attributeName)));
        public static bool HasAttribute(TypeDeclarationSyntax  cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => ((IdentifierNameSyntax)y.Name).Identifier.Text == attributeName));

        // Get all classes with the Aero attribute
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0 && HasAttribute(cds, "Aero")) {
                ClassesToAugment.Add(cds);
                AeroClasses.Add(cds);
                
                if (syntaxNode is ClassDeclarationSyntax cds2 && cds2.AttributeLists.Count > 0 && HasAttribute(cds2, AeroMessageIdAttribute.Name)) {
                    var aeroMsgInfo = AgUtils.GetAeroMessageIdAttributeInfo(cds2);
                    var asString    = aeroMsgInfo.GetAsString();

                    aeroMsgInfo.FullClassName = cds2.GetFullName();

                    if (AeroMessageIds.ContainsKey(asString)) {
                        Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.MultipleMessageIdsForTheSameType, cds2.GetLocation(), asString));
                    }
                    else {
                        AeroMessageIds.Add(asString, aeroMsgInfo);
                    }
                }
            }
            
            if (syntaxNode is StructDeclarationSyntax sds && sds.AttributeLists.Count > 0 && HasAttribute(sds, AeroBlockAttribute.Name)) {
                AeroBlockLookup.Add(sds.GetFullName(), sds);
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