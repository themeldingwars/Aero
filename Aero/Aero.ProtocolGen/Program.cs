namespace Aero.ProtocolGen
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string dumps = null, outDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dumps" && i + 1 < args.Length) dumps = args[++i];
                else if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
                else
                {
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    Console.Error.WriteLine("Usage: Aero.ProtocolGen [--dumps <dir>] [--out <dir>]");
                    return 2;
                }
            }

            // Defaults are relative to this project's source directory:
            //   Aero/Aero.ProtocolGen -> Aero/Aero.Gen/Lib/Sift/Dumps and Aero/Aero.Gen/Protocol
            var solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            dumps ??= Path.Combine(solutionDir, "Aero.Gen", "Lib", "Sift", "Dumps");
            outDir ??= Path.Combine(solutionDir, "Aero.Gen", "Protocol");

            if (!Directory.Exists(dumps))
            {
                Console.Error.WriteLine($"Dumps directory not found: {dumps}");
                return 1;
            }

            var result = ProtocolGen.Generate(dumps, outDir);

            Console.WriteLine($"Matrix : {result.MatrixVersionCount} versions, {result.MatrixMessageCount} messages");
            Console.WriteLine($"GSS    : {result.GssVersionCount} versions, {result.GssNamespaceCount} namespaces, {result.GssMessageCount} message/command members");
            Console.WriteLine($"Patches: {result.PatchCount} client builds");
            Console.WriteLine($"Dump hash: {result.DumpHash}");
            foreach (var f in result.WrittenFiles) Console.WriteLine($"  wrote     {f}");
            foreach (var f in result.UnchangedFiles) Console.WriteLine($"  unchanged {f}");
            return 0;
        }
    }
}
