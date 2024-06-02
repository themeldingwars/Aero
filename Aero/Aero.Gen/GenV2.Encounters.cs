using System;
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

                if (node is AeroFieldNode { IsEnum: true } fieldNode)
                {
                    typeStr = TypeAlias(fieldNode.EnumStr).ToLower();
                }

                byte byteType = typeStr switch
                {
                    "uint"                               => 0,
                    "float"                              => 1,
                    string t when t.EndsWith("entityid") => 2,
                    "ulong"                              => 3,
                    "byte"                               => 4,
                    // there's no type 5
                    "ushort"                             => 6,
                    string t when t.EndsWith("timer")    => 7,
                    "bool"                               => 8,

                    "uint[]"                               => 128,
                    "float[]"                              => 129,
                    string t when t.EndsWith("entityid[]") => 130,
                    "ulong[]"                              => 131,
                    "byte[]"                               => 132,
                    // there's no type 133 either
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
    }
}
