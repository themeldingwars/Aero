using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Aero.Gen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aero.UnitTests
{
    // Runs the generator on a source string, for tests that need other config options than
    // the ones in .globalconfig or that check the diagnostics the generator reports
    public static class GeneratorHarness
    {
        public class Result
        {
            public ImmutableArray<Diagnostic> GeneratorDiagnostics;
            public ImmutableArray<Diagnostic> CompileDiagnostics;
            public Assembly                   Assembly;

            public IEnumerable<string> GeneratorDiagnosticIds => GeneratorDiagnostics.Select(x => x.Id);

            public IEnumerable<Diagnostic> CompileErrors =>
                CompileDiagnostics.Where(x => x.Severity == DiagnosticSeverity.Error);

            public object Create(string typeName) => Activator.CreateInstance(Assembly.GetType(typeName, true));
        }

        public static Result Run(string source, bool diagLogging = false)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                            .Split(Path.PathSeparator)
                            .Select(x => MetadataReference.CreateFromFile(x))
                            .Append(MetadataReference.CreateFromFile(typeof(AeroGenerator).Assembly.Location));

            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var compilation = CSharpCompilation.Create($"AeroHarness{Guid.NewGuid():N}",
                new[] {CSharpSyntaxTree.ParseText(source, parseOptions)},
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var options = new HarnessOptionsProvider(new Dictionary<string, string>
            {
                {"aero_enabled", "true"},
                {"aero_diaglogging", diagLogging ? "true" : "false"}
            });

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] {new AeroGenerator()}, parseOptions: parseOptions,
                optionsProvider: options);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

            var result = new Result
            {
                GeneratorDiagnostics = generatorDiagnostics,
                CompileDiagnostics   = output.GetDiagnostics()
            };

            if (!result.CompileErrors.Any()) {
                using var ms = new MemoryStream();
                var emitResult = output.Emit(ms);
                if (!emitResult.Success) {
                    throw new InvalidOperationException("Emit failed: " + string.Join(", ", emitResult.Diagnostics));
                }

                ms.Position     = 0;
                result.Assembly = new AssemblyLoadContext(null, true).LoadFromStream(ms);
            }

            return result;
        }

        private class HarnessOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly HarnessOptions options;

            public HarnessOptionsProvider(Dictionary<string, string> values) => options = new HarnessOptions(values);

            public override AnalyzerConfigOptions GlobalOptions => options;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
        }

        private class HarnessOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> values;

            public HarnessOptions(Dictionary<string, string> values) => this.values = values;

            public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value);
        }
    }
}
