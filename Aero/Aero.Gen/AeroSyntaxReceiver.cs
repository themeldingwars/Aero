using System.Collections.Generic;
using System.Linq;
using Aero.Gen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class AeroSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> ClassesToAugment { get; private set; } = new();
        
        public List<ClassDeclarationSyntax> AeroClasses { get; private set; } = new();
        public List<StructDeclarationSyntax> AeroBlocks { get; private set; } = new();
        
        public static bool HasAttribute(ClassDeclarationSyntax cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => (y.Name is IdentifierNameSyntax ins && ins.Identifier.Text == attributeName) ||
                                                                                                                                               (y.Name is QualifiedNameSyntax qns && qns.ToString() == attributeName)));
        public static bool HasAttribute(TypeDeclarationSyntax  cds, string attributeName) => cds.AttributeLists.Any(x => x.Attributes.Any(y => ((IdentifierNameSyntax)y.Name).Identifier.Text == attributeName));

        // Get all classes with the Aero attribute
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0 && HasAttribute(cds, "Aero")) {
                ClassesToAugment.Add(cds);
                AeroClasses.Add(cds);
            }
            
            if (syntaxNode is StructDeclarationSyntax sds && sds.AttributeLists.Count > 0 && HasAttribute(sds, AeroBlockAttribute.Name)) {
                AeroBlocks.Add(sds);
            }
        }

        public StructDeclarationSyntax GetAeroBLockOfName(string ns, string name)
        {
            foreach (var ab in AeroBlocks) {
                if (ab.Identifier.Text == name) {
                    return ab;
                }
            }

            return null;
        }
    }
}