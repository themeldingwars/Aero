using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
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

    #endregion

        // These types have special case handlers to be treated like simpler value types
        public static readonly string[] SpecialCasesTypes = new[] { "system.numerics.vector2", "system.numerics.vector3", "system.numerics.vector4", "system.numerics.quaternion" };

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AeroSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                var config = AeroGenConfig.Load(context.AnalyzerConfigOptions.GlobalOptions);
                var snRecv = (AeroSyntaxReceiver) context.SyntaxReceiver;
                snRecv.Context = context;

                if (config.Enabled) {
                    // Aero message classes
                    foreach (var cls in snRecv.ClassesToAugment) {
                        var treeRoot = AeroSourceGraphGen.BuildTree(snRecv, cls);
                        Debug.Write(AeroSourceGraphGen.PrintTree(treeRoot));

                        var genv2 = new Genv2(context, config);
                        (string file, string src) = genv2.GenClass(cls);
                        //Debug.Write(src);
                        var name = Path.GetFileNameWithoutExtension(file);
                        //File.WriteAllText($"I:/AeroGenOutputTest/{name}.cs", src);
                        
                        context.AddSource(file, SourceText.From(src, Encoding.UTF8));
                    }
                }
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(GenericError, Location.None, $"{e.ToString()} {e.Source}"));
            }
        }
    }
}