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

        public static readonly DiagnosticDescriptor InvalidTypeWarning = new DiagnosticDescriptor(id: "AeroGen001",
            title: "Unsupported type",
            messageFormat: "'{0}' isn't supported for serialisation, sorry :<",
            category: "Aero.Gen",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    #endregion

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AeroSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var config = AeroGenConfig.Load(context.AnalyzerConfigOptions.GlobalOptions);
            var snRecv = (AeroSyntaxReceiver) context.SyntaxReceiver;

            if (config.Enabled) {
                // Aero message classes
                foreach (var cls in snRecv.ClassesToAugment) {
                    var genv2 = new Genv2(context, config);
                    (string file, string src) = genv2.GenClass(cls);
                    Debug.Write(src);
                    context.AddSource(file, SourceText.From(src, Encoding.UTF8));
                }
            }
        }
    }
}