﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
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

        public static readonly DiagnosticDescriptor StructNotMarkedAsAeroBlockError = new DiagnosticDescriptor(id: $"Aero4",
            title: "Included struct isn't marked as an AeroBlock",
            messageFormat: "Field '{0}' uses type '{1}' that isn't marked as an AeroBlock, please add the attribute for this to get serialised",
            category: "Aero.Gen",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

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

                if (config.Enabled) {
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

            void AddPacketSwitch(IEnumerable<AeroMessageIdAttribute> msgs)
            {
                AddLineAndIndent(sb, "IAero msg = messageId switch {");
                {
                    foreach (var msg in msgs) {
                        AddLine(sb, $"{msg.MessageId} => new {msg.FullClassName}(),");
                    }
                    
                    AddLine(sb, $"_ => null,");
                }
                UnIndentAndAddLine(sb, "};");
                AddLine(sb, "");
                AddLine(sb, "return msg;");
            }

            AddLine(sb, "using System;");
            AddLine(sb, "using Aero.Gen;");
            AddLine(sb, "using Aero.Gen.Attributes;");

            AddLine(sb, $"public static class AeroRouting");
            AddLine(sb, "{");
            Indent();
            {
                AddLine(sb, $"public static IAero GetNewMessageHandler(AeroMessageIdAttribute.MsgType typ, AeroMessageIdAttribute.MsgSrc src, int messageId, int controllerId = -1)");
                AddLineAndIndent(sb, "{");
                {
                    AddLine(sb, "if (typ == AeroMessageIdAttribute.MsgType.Control)");
                    AddLineAndIndent(sb, "{");
                    {
                        var controllMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Control);
                        AddPacketSwitch(controllMsgs);

                        UnIndentAndAddLine(sb, "}");
                        AddLine(sb, "else if (typ == AeroMessageIdAttribute.MsgType.Matrix)");
                        AddLineAndIndent(sb, "{");
                        {
                            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Command || src == AeroMessageIdAttribute.MsgSrc.Both) ");
                            AddLineAndIndent(sb, "{");
                            {
                                var msgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Matrix && x.Src is AeroMessageIdAttribute.MsgSrc.Command or AeroMessageIdAttribute.MsgSrc.Both);
                                AddPacketSwitch(msgs);
                            }
                            UnIndentAndAddLine(sb, "}");

                            AddLine(sb, "else if (src == AeroMessageIdAttribute.MsgSrc.Message || src == AeroMessageIdAttribute.MsgSrc.Both)");
                            AddLineAndIndent(sb, "{");
                            {
                                var msgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.Matrix && x.Src is AeroMessageIdAttribute.MsgSrc.Message or AeroMessageIdAttribute.MsgSrc.Both);
                                AddPacketSwitch(msgs);
                            }
                            UnIndentAndAddLine(sb, "}");
                        }
                        UnIndentAndAddLine(sb, "}");

                        AddLine(sb, "else if (typ == AeroMessageIdAttribute.MsgType.GSS)");
                        AddLineAndIndent(sb, "{");
                        {
                            AddLine(sb, "if (src == AeroMessageIdAttribute.MsgSrc.Command || src == AeroMessageIdAttribute.MsgSrc.Both)");
                            AddLineAndIndent(sb, "{");
                            {
                                var controllerMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.GSS &&
                                                                                             x.Src is AeroMessageIdAttribute.MsgSrc.Command or AeroMessageIdAttribute.MsgSrc.Both).GroupBy(x => x.ControllerId);
                                foreach (var msgs in controllerMsgs) {
                                    AddLineAndIndent(sb, $"if (controllerId == {msgs.First().ControllerId}) {{");
                                    {
                                        AddPacketSwitch(msgs);
                                    }
                                    UnIndentAndAddLine(sb, "}");
                                }
                            }
                            UnIndentAndAddLine(sb, "}");

                            AddLine(sb, "else if (src == AeroMessageIdAttribute.MsgSrc.Message || src == AeroMessageIdAttribute.MsgSrc.Both)");
                            AddLineAndIndent(sb, "{");
                            {
                                var controllerMsgs = snRecv.AeroMessageIds.Values.Where(x => x.Typ == AeroMessageIdAttribute.MsgType.GSS &&
                                                                                             x.Src is AeroMessageIdAttribute.MsgSrc.Message or AeroMessageIdAttribute.MsgSrc.Both).GroupBy(x => x.ControllerId);
                                foreach (var msgs in controllerMsgs) {
                                    AddLineAndIndent(sb, $"if (controllerId == {msgs.First().ControllerId}) {{");
                                    {
                                        AddPacketSwitch(msgs);
                                    }
                                    UnIndentAndAddLine(sb, "}");
                                }
                            }
                            UnIndentAndAddLine(sb, "}");
                        }
                        UnIndent();
                        AddLine(sb, "}");
                        AddLine(sb, "");
                        AddLine(sb, "return null;");
                        
                        UnIndentAndAddLine(sb, "}");
                        UnIndentAndAddLine(sb, "}");
                        return sb.ToString();
                    }
                }
            }
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
