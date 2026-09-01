using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Aero.Gen.Attributes;
using Aero.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aero.Gen
{
    [Generator]
    public class AeroGenerator : ISourceGenerator
    {
    #region Diag Errors

        public static int AeroDiagId = 1;

        public static readonly DiagnosticDescriptor InvalidTypeWarning = new DiagnosticDescriptor(id: "Aero1",
            title: "Unsupported type",
            messageFormat: "'{0}' isn't supported for serialisation, sorry :<",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericError = new DiagnosticDescriptor(
            id: $"Aero2",
            title: "An exception was thrown by the Aero.Gen generator",
            messageFormat: "An exception was thrown by the Aero.Gen generator: '{0}'",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericInfo = new DiagnosticDescriptor(id: $"Aero3",
            title: "GenericInfo",
            messageFormat: "'{0}'",
            category: "Aero.Gen",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NoArrayAttributeError = new DiagnosticDescriptor(id: $"Aero4",
            title: "Array doesn't have an AeroArray attribute",
            messageFormat: "Field '{0}' doesn't have an AeroArray attribute, you need to add one to tell me how to handle this! D:",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

#pragma warning disable RS2001
        public static readonly DiagnosticDescriptor StructNotMarkedAsAeroBlockError = new DiagnosticDescriptor(id: $"Aero4",
            title: "Included struct isn't marked as an AeroBlock",
            messageFormat: "Field '{0}' uses type '{1}' that isn't marked as an AeroBlock, please add the attribute for this to get serialised",
            category: "Aero.Gen",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
#pragma warning restore RS2001

        public static readonly DiagnosticDescriptor ClassNotAllowedInAeroError = new DiagnosticDescriptor(id: $"Aero5",
            title: "Class no allowed",
            messageFormat: "Field '{0}' uses type '{1}' that is a class and not a struct so can't be used, sorry :<",
            category: "Aero.Gen",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleMessageIdsForTheSameType = new DiagnosticDescriptor(id: "Aero6",
            title: "Multiple MessageIds For The Same Type",
            messageFormat: "There already is a class marked with this message id, '{0}' ",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidMessageIdError = new DiagnosticDescriptor(id: "Aero9",
            title: "Invalid AeroMessageId attribute",
            messageFormat: "Invalid AeroMessageId attribute: {0}",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor VersionRangeOverlapError = new DiagnosticDescriptor(id: "Aero10",
            title: "Overlapping AeroMessageId version ranges",
            messageFormat: "AeroMessageId {0} on {1} (range {2}) overlaps with {3} on {4} (range {5})",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidTypeInEncounterView = new DiagnosticDescriptor(id: "Aero7",
            title: "Invalid type in encounter view",
            messageFormat: "Field '{0}' uses invalid type '{1}', use byte/bool/ushort/uint/float/ulong/Timer/EntityId or fixed size array of any of these types",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidArrayModeInEncounterView = new DiagnosticDescriptor(id: "Aero8",
            title: "Invalid array mode in encounter view",
            messageFormat: "Field '{0}' uses array mode '{1}', but only fixed size arrays are supported in encounter views",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    #endregion

        public static FieldDeclarationSyntax LastCheckedField;

        // These types have special case handlers to be treated like simpler value types
        public static readonly string[] SpecialCasesTypes = new[] {"system.numerics.vector2", "system.numerics.vector3", "system.numerics.vector4", "system.numerics.quaternion"};

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AeroSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            string lastClassGenerated = "";

            try {
                var config = AeroGenConfig.Load(context.AnalyzerConfigOptions.GlobalOptions);
                var snRecv = (AeroSyntaxReceiver) context.SyntaxReceiver;
                snRecv.Context = context;

                if (config.Enabled && snRecv.ClassesToAugment.Count > 0) {
                    snRecv.ResolveMessageIds(context);

                    // Aero message classes
                    foreach (var cls in snRecv.ClassesToAugment) {
                        var treeRoot = AeroSourceGraphGen.BuildTree(snRecv, cls);
                        Debug.Write(AeroSourceGraphGen.PrintTree(treeRoot));

                        lastClassGenerated = AgUtils.GetClassName(cls);

                        var genv2 = new Genv2(context, config);
                        (string file, string src) = genv2.GenClass(cls);
                        //Debug.Write(src);
                        var name = Path.GetFileNameWithoutExtension(file);
                        //File.WriteAllText($"I:/AeroGenOutputTest/{name}.cs", src);

                        context.AddSource(file, SourceText.From(src, Encoding.UTF8));
                    }

                    var routing = CreateRouting(snRecv, config);
                    Debug.WriteLine(routing);
                    context.AddSource("AeroRouting.cs", SourceText.From(routing, Encoding.UTF8));

                    var encounters = CreateEncounters(snRecv);
                    context.AddSource("AeroEncounters.cs", SourceText.From(encounters, Encoding.UTF8));
                }
            }
            catch (Exception e) {
                context.ReportDiagnostic(Diagnostic.Create(GenericError, LastCheckedField != default ? LastCheckedField.GetLocation() : Location.None, $"Error processing file {lastClassGenerated}: {e.ToString()} {e.Source} Trace: {e.StackTrace}"));
            }
        }

        protected int IndentLevel = 0;

        protected static int  TabSpaces = 4;
        public           void Indent()                               => IndentLevel += TabSpaces;
        public           void UnIndent()                             => IndentLevel -= TabSpaces;
        public           void AddLine(StringBuilder sb, string line) => sb.AppendLine($"{new string(' ', Math.Max(IndentLevel, 0))}{line}");

        public void AddLineAndIndent(StringBuilder sb, string line)
        {
            AddLine(sb, line);
            Indent();
        }

        public void UnIndentAndAddLine(StringBuilder sb, string line)
        {
            UnIndent();
            AddLine(sb, line);
        }

        public void AddLine(StringBuilder  sb)                        => AddLine(sb, "");
        public void AddLines(StringBuilder sb, params string[] lines) => Array.ForEach(lines, (line) => AddLine(sb, line));

        private string CreateRouting(AeroSyntaxReceiver snRecv, AeroGenConfig config)
        {
            var sb = new StringBuilder();

            void EmitVersionRangeChecks(IEnumerable<AeroMessageIdAttribute> msgs, string versionVar)
            {
                foreach (var msg in msgs.OrderBy(x => x.VersionFrom).ThenBy(x => x.VersionTo))
                    AddLine(sb, $"if ({versionVar} >= ({msg.VersionEnum}){msg.VersionFrom} && {versionVar} <= ({msg.VersionEnum}){msg.VersionTo}) return new {msg.FullClassName}();");
            }

            void EmitMatrixOrdinalSwitch(IEnumerable<AeroMessageIdAttribute> msgs, string versionVar)
            {
                AddLine(sb, $"int ordinal = MatrixTables.FindMessage({versionVar}, messageId);");
                AddLineAndIndent(sb, "switch (ordinal)");
                AddLineAndIndent(sb, "{");
                foreach (var group in msgs.GroupBy(x => x.MessageOrdinal).OrderBy(g => g.Key))
                {
                    var first = group.First();
                    AddLine(sb, $"case (int){first.MessageEnum}.{first.MessageName}:");
                    Indent();
                    EmitVersionRangeChecks(group, versionVar);
                    AddLine(sb, "break;");
                    UnIndent();
                }
                AddLine(sb, "default: break;");
                UnIndentAndAddLine(sb, "}");
            }

            void EmitGssKindBlock(int kind, string kindName, Dictionary<(int Ns, int View, int Ordinal), List<AeroMessageIdAttribute>> groups)
            {
                // The typecode may be a view route or the plain namespace route; messages registered
                // with a view only match their own view route, view-less ones only the namespace route.
                AddLine(sb, "int ns;");
                AddLine(sb, "int viewOrdinal = -1;");
                AddLine(sb, "if (GssTables.TryFindView(gssVersion, typecode, out int routeNs, out int routeView))");
                AddLineAndIndent(sb, "{");
                AddLine(sb, "ns = routeNs;");
                AddLine(sb, "viewOrdinal = routeView;");
                UnIndentAndAddLine(sb, "}");
                AddLine(sb, "else");
                AddLineAndIndent(sb, "{");
                AddLine(sb, $"ns = GssTables.FindNamespace(gssVersion, typecode, GssTables.Kind.{kindName});");
                UnIndentAndAddLine(sb, "}");
                AddLine(sb, "if (ns != GssTables.Ns.Unknown)");
                AddLineAndIndent(sb, "{");
                AddLine(sb, $"int ordinal = GssTables.FindMessage(gssVersion, typecode, GssTables.Kind.{kindName}, messageId);");
                AddLineAndIndent(sb, "switch (ns)");
                AddLineAndIndent(sb, "{");
                foreach (var nsGroup in groups.GroupBy(x => x.Key.Ns).OrderBy(g => g.Key))
                {
                    AddLine(sb, $"case {nsGroup.Key}:");
                    Indent();
                    var nsAttrs = nsGroup.SelectMany(x => x.Value).ToList();
                    AddLineAndIndent(sb, "switch (viewOrdinal)");
                    AddLineAndIndent(sb, "{");
                    foreach (var viewGroup in nsAttrs.GroupBy(x => x.ViewOrdinal).OrderBy(g => g.Key))
                    {
                        var first = viewGroup.First();
                        var viewCase = first.ViewOrdinal >= 0 ? $"(int){first.ViewEnum}.{first.ViewName}" : "-1";
                        AddLine(sb, $"case {viewCase}:");
                        Indent();
                        AddLineAndIndent(sb, "switch (ordinal)");
                        AddLineAndIndent(sb, "{");
                        foreach (var ordinalGroup in viewGroup.GroupBy(x => x.MessageOrdinal).OrderBy(g => g.Key))
                        {
                            var of = ordinalGroup.First();
                            AddLine(sb, $"case (int){of.MessageEnum}.{of.MessageName}:");
                            Indent();
                            EmitVersionRangeChecks(ordinalGroup, "gssVersion");
                            AddLine(sb, "break;");
                            UnIndent();
                        }
                        AddLine(sb, "default: break;");
                        UnIndentAndAddLine(sb, "}");
                        AddLine(sb, "break;");
                        UnIndent();
                    }
                    AddLine(sb, "default: break;");
                    UnIndentAndAddLine(sb, "}");
                    AddLine(sb, "break;");
                    UnIndent();
                }
                AddLine(sb, "default: break;");
                UnIndentAndAddLine(sb, "}");
                UnIndentAndAddLine(sb, "}");
            }

            void EmitGssViewBlock(Dictionary<(int Ns, int Ordinal), List<AeroMessageIdAttribute>> groups)
            {
                AddLine(sb, "if (GssTables.TryFindView(gssVersion, typecode, out int vns, out int vord))");
                AddLineAndIndent(sb, "{");
                AddLineAndIndent(sb, "switch (vns)");
                AddLineAndIndent(sb, "{");
                foreach (var nsGroup in groups.GroupBy(x => x.Key.Ns).OrderBy(g => g.Key))
                {
                    AddLine(sb, $"case {nsGroup.Key}:");
                    Indent();
                    AddLineAndIndent(sb, "switch (vord)");
                    AddLineAndIndent(sb, "{");
                    foreach (var ordinalGroup in nsGroup.SelectMany(x => x.Value).GroupBy(x => x.MessageOrdinal).OrderBy(g => g.Key))
                    {
                        var first = ordinalGroup.First();
                        AddLine(sb, $"case (int){first.MessageEnum}.{first.MessageName}:");
                        Indent();
                        EmitVersionRangeChecks(ordinalGroup, "gssVersion");
                        AddLine(sb, "break;");
                        UnIndent();
                    }
                    AddLine(sb, "default: break;");
                    UnIndentAndAddLine(sb, "}");
                    AddLine(sb, "break;");
                    UnIndent();
                }
                AddLine(sb, "default: break;");
                UnIndentAndAddLine(sb, "}");
                UnIndentAndAddLine(sb, "}");
            }

            var controlMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Control).OrderBy(x => x.MessageId).ToList();
            var matrixCommandMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Matrix && !x.IsControl && (x.Src == AeroMessageIdAttribute.MsgSrc.Command || x.Src == AeroMessageIdAttribute.MsgSrc.Both)).ToList();
            var matrixMessageMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Matrix && !x.IsControl && (x.Src == AeroMessageIdAttribute.MsgSrc.Message || x.Src == AeroMessageIdAttribute.MsgSrc.Both)).ToList();

            var gssCommandGroups = new Dictionary<(int Ns, int View, int Ordinal), List<AeroMessageIdAttribute>>();
            var gssMessageGroups = new Dictionary<(int Ns, int View, int Ordinal), List<AeroMessageIdAttribute>>();
            var gssViewGroups    = new Dictionary<(int Ns, int Ordinal), List<AeroMessageIdAttribute>>();
            foreach (var msg in snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.GSS && !x.IsControl))
            {
                if (!GssTables.TryGetProtocolEnumInfo(msg.MessageEnum, out int ns, out int kind))
                    continue;

                if (kind == GssTables.Kind.Command && (msg.Src == AeroMessageIdAttribute.MsgSrc.Command || msg.Src == AeroMessageIdAttribute.MsgSrc.Both))
                {
                    var key = (ns, msg.ViewOrdinal, msg.MessageOrdinal);
                    if (!gssCommandGroups.TryGetValue(key, out var list))
                        gssCommandGroups[key] = list = new List<AeroMessageIdAttribute>();
                    list.Add(msg);
                }
                if (kind == GssTables.Kind.Message && (msg.Src == AeroMessageIdAttribute.MsgSrc.Message || msg.Src == AeroMessageIdAttribute.MsgSrc.Both))
                {
                    var key = (ns, msg.ViewOrdinal, msg.MessageOrdinal);
                    if (!gssMessageGroups.TryGetValue(key, out var list))
                        gssMessageGroups[key] = list = new List<AeroMessageIdAttribute>();
                    list.Add(msg);
                }
                if (kind == GssTables.Kind.View && (msg.Src == AeroMessageIdAttribute.MsgSrc.Message || msg.Src == AeroMessageIdAttribute.MsgSrc.Both))
                {
                    if (!gssViewGroups.TryGetValue((ns, msg.MessageOrdinal), out var list))
                        gssViewGroups[(ns, msg.MessageOrdinal)] = list = new List<AeroMessageIdAttribute>();
                    list.Add(msg);
                }
            }

            AddLine(sb, "using System;");
            AddLine(sb, "using Aero.Gen;");
            AddLine(sb, "using Aero.Gen.Attributes;");
            AddLine(sb, "using Aero.Protocol;");
            AddLine(sb, "");
            AddLine(sb, "public static class AeroRouting");
            AddLine(sb, "{");
            Indent();
            AddLine(sb, "public static MatrixVersion CurrentMatrixVersion = MatrixVersion.V1;");
            AddLine(sb, "public static GssVersion CurrentGssVersion = GssVersion.V1;");
            AddLine(sb, "");
            AddLine(sb, "public static IAero GetNewMessageHandler(AeroMessageIdAttribute.MsgType typ, AeroMessageIdAttribute.MsgSrc src, byte messageId, byte typecode = 0)");
            AddLine(sb, "    => GetNewMessageHandlerInternal(typ, src, messageId, typecode, CurrentMatrixVersion, CurrentGssVersion);");
            AddLine(sb, "");

            if (matrixCommandMsgs.Any() || matrixMessageMsgs.Any())
            {
                AddLine(sb, "public static IAero GetNewMessageHandler(MatrixVersion version, AeroMessageIdAttribute.MsgSrc src, MatrixMessage message)");
                AddLineAndIndent(sb, "{");
                AddLine(sb, "byte messageId = MatrixTables.GetMessageId(version, message);");
                AddLine(sb, "if (messageId == 0) return null;");
                AddLine(sb, "return GetNewMessageHandlerInternal(AeroMessageIdAttribute.MsgType.Matrix, src, messageId, 0, version, CurrentGssVersion);");
                UnIndentAndAddLine(sb, "}");
                AddLine(sb, "");
            }

            var gssDirectEnums = snRecv.AeroMessageIds.Values
                .Where(x => x.Typ == AeroMessageIdAttribute.MsgType.GSS && !x.IsControl)
                .Select(x => x.MessageEnum)
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            var gssViewDirectPairs = snRecv.AeroMessageIds.Values
                .Where(x => x.Typ == AeroMessageIdAttribute.MsgType.GSS && !x.IsControl && x.ViewOrdinal >= 0)
                .Select(x => (x.MessageEnum, x.ViewEnum))
                .Distinct()
                .OrderBy(x => x.MessageEnum, StringComparer.Ordinal)
                .ThenBy(x => x.ViewEnum, StringComparer.Ordinal)
                .ToList();
            foreach (var gssEnum in gssDirectEnums)
            {
                if (!GssTables.TryGetProtocolEnumInfo(gssEnum, out int directNs, out int directKind))
                    continue;

                if (directKind == GssTables.Kind.View)
                {
                    AddLine(sb, $"public static IAero GetNewMessageHandler(GssVersion version, AeroMessageIdAttribute.MsgSrc src, {gssEnum} view)");
                    AddLineAndIndent(sb, "{");
                    AddLine(sb, $"byte typecode = GssTables.GetMessageId(version, {directNs}, GssTables.Kind.View, (int)view);");
                    AddLine(sb, "if (typecode == 0) return null;");
                    AddLine(sb, "return GetNewMessageHandlerInternal(AeroMessageIdAttribute.MsgType.GSS, src, 0, typecode, CurrentMatrixVersion, version);");
                    UnIndentAndAddLine(sb, "}");
                    AddLine(sb, "");
                }
                else
                {
                    string kindName = directKind == GssTables.Kind.Message ? "Message" : "Command";
                    AddLine(sb, $"public static IAero GetNewMessageHandler(GssVersion version, AeroMessageIdAttribute.MsgSrc src, {gssEnum} message)");
                    AddLineAndIndent(sb, "{");
                    AddLine(sb, $"byte messageId = GssTables.GetMessageId(version, {directNs}, GssTables.Kind.{kindName}, (int)message);");
                    AddLine(sb, "byte typecode = GssTables.GetNamespaceTypecode(version, " + directNs + ");");
                    AddLine(sb, "if (messageId == 0 || typecode == 255) return null;");
                    AddLine(sb, "return GetNewMessageHandlerInternal(AeroMessageIdAttribute.MsgType.GSS, src, messageId, typecode, CurrentMatrixVersion, version);");
                    UnIndentAndAddLine(sb, "}");
                    AddLine(sb, "");
                    foreach (var pair in gssViewDirectPairs.Where(p => p.MessageEnum == gssEnum))
                    {
                        AddLine(sb, $"public static IAero GetNewMessageHandler(GssVersion version, AeroMessageIdAttribute.MsgSrc src, {gssEnum} message, {pair.ViewEnum} view)");
                        AddLineAndIndent(sb, "{");
                        AddLine(sb, $"byte messageId = GssTables.GetMessageId(version, {directNs}, GssTables.Kind.{kindName}, (int)message);");
                        AddLine(sb, $"byte typecode = GssTables.GetMessageId(version, {directNs}, GssTables.Kind.View, (int)view);");
                        AddLine(sb, "if (messageId == 0 || typecode == 0) return null;");
                        AddLine(sb, "return GetNewMessageHandlerInternal(AeroMessageIdAttribute.MsgType.GSS, src, messageId, typecode, CurrentMatrixVersion, version);");
                        UnIndentAndAddLine(sb, "}");
                        AddLine(sb, "");
                    }
                }
            }

            AddLine(sb, "public static MatrixMessage? GetMatrixMessage(MatrixVersion version, byte messageId)");
            AddLineAndIndent(sb, "{");
            AddLine(sb, "int ordinal = MatrixTables.FindMessage(version, messageId);");
            AddLine(sb, "return ordinal < 0 ? null : (MatrixMessage)ordinal;");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "");
            AddLine(sb, "public static int GetGssMessageOrdinal(GssVersion version, byte typecode, int kind, byte messageId)");
            AddLine(sb, "    => GssTables.FindMessage(version, typecode, kind, messageId);");
            AddLine(sb, "");
            AddLine(sb, "public static (byte Typecode, byte MessageId) GetGssMessageId(GssVersion version, int nsIndex, int kind, int ordinal)");
            AddLineAndIndent(sb, "{");
            AddLine(sb, "byte typecode = GssTables.GetNamespaceTypecode(version, nsIndex);");
            AddLine(sb, "byte messageId = GssTables.GetMessageId(version, nsIndex, kind, ordinal);");
            AddLine(sb, "return (typecode, messageId);");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "");

            AddLine(sb, "private static IAero GetNewMessageHandlerInternal(AeroMessageIdAttribute.MsgType typ, AeroMessageIdAttribute.MsgSrc src, byte messageId, byte typecode, MatrixVersion matrixVersion, GssVersion gssVersion)");
            AddLineAndIndent(sb, "{");
            AddLine(sb, "if (typ == AeroMessageIdAttribute.MsgType.Control)");
            AddLineAndIndent(sb, "{");
            AddLineAndIndent(sb, "IAero msg = messageId switch {");
            foreach (var msg in controlMsgs)
                AddLine(sb, $"{msg.MessageId} => new {msg.FullClassName}(),");
            AddLine(sb, "_ => null,");
            UnIndentAndAddLine(sb, "};");
            AddLine(sb, "return msg;");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "else if (typ == AeroMessageIdAttribute.MsgType.Matrix)");
            AddLineAndIndent(sb, "{");
            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Command || src == AeroMessageIdAttribute.MsgSrc.Both)");
            AddLineAndIndent(sb, "{");
            EmitMatrixOrdinalSwitch(matrixCommandMsgs, "matrixVersion");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Message || src == AeroMessageIdAttribute.MsgSrc.Both)");
            AddLineAndIndent(sb, "{");
            EmitMatrixOrdinalSwitch(matrixMessageMsgs, "matrixVersion");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "return null;");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "else if (typ == AeroMessageIdAttribute.MsgType.GSS)");
            AddLineAndIndent(sb, "{");
            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Command || src == AeroMessageIdAttribute.MsgSrc.Both)");
            AddLineAndIndent(sb, "{");
            EmitGssKindBlock(GssTables.Kind.Command, "Command", gssCommandGroups);
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Message || src == AeroMessageIdAttribute.MsgSrc.Both)");
            AddLineAndIndent(sb, "{");
            EmitGssKindBlock(GssTables.Kind.Message, "Message", gssMessageGroups);
            if (gssViewGroups.Count > 0)
                EmitGssViewBlock(gssViewGroups);
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "return null;");
            UnIndentAndAddLine(sb, "}");
            AddLine(sb, "return null;");
            UnIndentAndAddLine(sb, "}");
            UnIndentAndAddLine(sb, "}");

            return sb.ToString();
        }

        private string CreateEncounters(AeroSyntaxReceiver snRecv)
        {
            var sb = new StringBuilder();

            AddLine(sb, "using System;");
            AddLine(sb, "using Aero.Gen;");

            AddLine(sb, $"public static class AeroEncounters");
            AddLine(sb, "{");

            Indent();
            {
                AddLine(sb, "public static IAeroEncounter GetEncounterClass(string encounterType)");
                AddLineAndIndent(sb, "{");
                {
                    AddLineAndIndent(sb, "IAeroEncounter encounter = encounterType switch {");
                    {
                        foreach (var encounter in snRecv.AeroEncounterClasses)
                        {
                            var attr = AgUtils.NodeWithName<AttributeSyntax>(encounter, AeroEncounterAttribute.Name);

                            var type = attr.ArgumentList.Arguments[0].Expression.ToString();

                            AddLine(sb, $"{type} => new {encounter.GetFullName()}(),");
                        }

                        AddLine(sb, "_ => null,");
                    }

                    UnIndentAndAddLine(sb, "};");
                    AddLine(sb, "");
                    AddLine(sb, "return encounter;");
                }

                UnIndentAndAddLine(sb, "}");
                UnIndentAndAddLine(sb, "}");

                return sb.ToString();
            }
        }
    }
}
