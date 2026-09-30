using System;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public partial class Genv2
    {
        private void GenerateEncounterFunctions(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            GenerateEncounterHeader(cd);

            GenerateViewUpdateUnpacker(cd, sm);
            GenerateViewUpdatePacker(cd, sm);
            GenerateClearViewChanges(cd);
            GenerateGetPackedChangesSize(cd, sm);
        }

        private void GenerateEncounterHeader(ClassDeclarationSyntax cd)
        {
            var encounterAttr = AgUtils.NodeWithName<AttributeSyntax>(cd, AeroEncounterAttribute.Name);

            var encounterType = encounterAttr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');

            string NameToHex(string name)
            {
                var bytes = Encoding.UTF8.GetBytes(name + '\0');

                var hex = new StringBuilder();

                foreach (var b in bytes)
                {
                    hex.AppendFormat("0x{0:X2}, ", b);
                }

                return hex.ToString().Trim();
            }

            // Type 4 fields are followed by their value names: 0x01 before each name, a closing 0x01 and a 0
            string ValueNamesToHex(string[] names) =>
                NameToHex("\u0001" + string.Concat(names.Select(x => x + "\u0001")));

            var sm       = SyntaxReceiver.Context.Compilation.GetSemanticModel(cd.SyntaxTree);
            var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
            byte fieldIdx = 0;

            AddLine("public static readonly byte[] Header = {");
            Indent();

            AddLine($"{NameToHex(encounterType)} // {encounterType}");

            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                if (node.Depth != 0)
                {
                    return;
                }

                if (node is AeroBlobNode)
                {
                    Context.ReportDiagnostic(
                        Diagnostic.Create(AeroGenerator.AeroBlobInEncounterViewError, cd.GetLocation(), node.Name)
                    );
                    return;
                }

                var name = node.Name;
                byte count = 1;

                if (node is AeroArrayNode arr)
                {
                    if (arr.Mode != AeroArrayNode.Modes.Fixed)
                    {
                        Context.ReportDiagnostic(
                            Diagnostic.Create(AeroGenerator.InvalidArrayModeInEncounterView, cd.GetLocation(), name, arr.Mode)
                        );
                    }

                    name = arr.Nodes[0].Name;
                    count = (byte)arr.Length;
                }

                var typeStr = node.TypeStr.ToLower();
                var isArray = node is AeroArrayNode;
                string[] valueNames = null;

                var enumNode = (isArray ? node.Nodes[0] : node) as AeroFieldNode;
                if (enumNode is { IsEnum: true })
                {
                    typeStr = TypeAlias(enumNode.EnumStr).ToLower() + (isArray ? "[]" : "");

                    // A ushort enum is type 4, the value is an index into the value names listed after the field
                    if (enumNode.EnumStr == "uint16")
                    {
                        valueNames = GetEncounterEnumValueNames(cd, sm, name);
                        if (valueNames == null)
                        {
                            Context.ReportDiagnostic(
                                Diagnostic.Create(AeroGenerator.InvalidEnumInEncounterView, cd.GetLocation(), name, enumNode.TypeStr)
                            );
                        }

                        typeStr = isArray ? "enum[]" : "enum";
                    }
                }

                byte byteType = typeStr switch
                {
                    "uint"                               => 0,
                    "float"                              => 1,
                    string t when t.EndsWith("entityid") => 2,
                    "ulong"                              => 3,
                    "enum"                               => 4,
                    // there's no type 5
                    "short"                              => 6,
                    "ushort"                             => 6,
                    string t when t.EndsWith("timer")    => 7,
                    "bool"                               => 8,

                    "uint[]"                               => 128,
                    "float[]"                              => 129,
                    string t when t.EndsWith("entityid[]") => 130,
                    "ulong[]"                              => 131,
                    "enum[]"                               => 132,
                    // there's no type 133 either
                    "short[]"                              => 134,
                    "ushort[]"                             => 134,
                    string t when t.EndsWith("timer[]")    => 135,
                    "bool[]"                               => 136,

                    _                                      => 255,
                };

                if (byteType == 255)
                {
                    Context.ReportDiagnostic(
                        Diagnostic.Create(AeroGenerator.InvalidTypeInEncounterView, cd.GetLocation(), name, typeStr)
                    );
                }

                var hexIdx = BitConverter.ToString(new[]{ fieldIdx });
                var hexType = BitConverter.ToString(new[]{ byteType });
                var hexCount = BitConverter.ToString(new[]{ count });

                AddLine(
                    $"0x{hexIdx}, 0x{hexType}, 0x{hexCount}, // idx: {fieldIdx}, type: {typeStr}, count: {count}, name: {name}");
                AddLine(NameToHex(name));
                if (valueNames != null)
                {
                    AddLine($"{ValueNamesToHex(valueNames)} // value names: {string.Join(", ", valueNames)}");
                }

                fieldIdx++;
            });

            UnIndent();
            AddLine("};"); // end static Header

            using (Function("public byte[] GetHeader()"))
            {
                AddLine("return Header;");
            }

            AddLine();
        }

        // The names of the enum's values ordered by value, or null if there are none or they aren't 0 to n-1
        private static string[] GetEncounterEnumValueNames(ClassDeclarationSyntax cd, SemanticModel sm, string fieldName)
        {
            var field = AgUtils.GetClassFields(cd, true).FirstOrDefault(x => AgUtils.GetFieldName(x) == fieldName);
            var type  = field != null ? sm.GetTypeInfo(field.Declaration.Type).Type : null;
            if (type is IArrayTypeSymbol arrayType)
            {
                type = arrayType.ElementType;
            }

            if (type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            {
                return null;
            }

            var values = enumType.GetMembers()
                                 .OfType<IFieldSymbol>()
                                 .Where(x => x.HasConstantValue)
                                 .Select(x => (x.Name, Value: Convert.ToInt32(x.ConstantValue)))
                                 .OrderBy(x => x.Value)
                                 .ToArray();

            if (values.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Value != i)
                {
                    return null;
                }
            }

            return values.Select(x => x.Name).ToArray();
        }
    }
}
