using System.Security.Cryptography;
using System.Text;
using Sift;

namespace Aero.ProtocolGen
{
    public class GenerationResult
    {
        public int MatrixVersionCount;
        public int MatrixMessageCount;
        public int GssVersionCount;
        public int GssNamespaceCount;
        public int GssMessageCount;
        public int PatchCount;
        public string DumpHash;
        public List<string> WrittenFiles = new();
        public List<string> UnchangedFiles = new();
    }

    class MessageTable
    {
        public string[] Names; // union of names across all versions, first-appearance order
        public byte[,] Ids;    // [versionIndex, ordinal] -> wire id, 0 = absent
    }

    class MatrixData
    {
        public ushort[] Versions;
        public DateTime[] FirstBuild;
        public MessageTable Table;
    }

    class GssNamespace
    {
        public string Name;
        public MessageTable[] Tables = new MessageTable[3]; // [View, Message, Command]
    }

    class GssData
    {
        public ushort[] Versions;
        public DateTime[] FirstBuild;
        public MessageTable Root;
        public List<GssNamespace> Namespaces = new();
        public int[] RouteEntries = Array.Empty<int>(); // (versionIndex, routeId, packed) triples
    }

    public static class ProtocolGen
    {
        // GSS table kinds, must match Aero.Protocol.GssTables.Kind
        private const int View = 0, Message = 1, Command = 2;
        private static readonly string[] KindFieldNames = { "Views", "Messages", "Commands" };
        private static readonly string[] KindEnumNames = { "View", "Message", "Command" };

        public static GenerationResult Generate(string dumpsDir, string outDir)
        {
            var dump = Result.Load(dumpsDir);
            var hash = ComputeDumpHash(dumpsDir);

            var matrix = BuildMatrix(dump);
            var gss = BuildGss(dump);
            var patches = BuildPatches(dump);

            Directory.CreateDirectory(outDir);
            var gen = new GenerationResult
            {
                DumpHash = hash,
                MatrixVersionCount = matrix.Versions.Length,
                MatrixMessageCount = matrix.Table.Names.Length,
                GssVersionCount = gss.Versions.Length,
                GssNamespaceCount = gss.Namespaces.Count,
                GssMessageCount = gss.Root.Names.Length
                    + gss.Namespaces.Sum(n => n.Tables[Message]?.Names.Length ?? 0)
                    + gss.Namespaces.Sum(n => n.Tables[Command]?.Names.Length ?? 0),
                PatchCount = patches.Count,
            };

            Write(outDir, "Aero.Protocol.Versions.cs", EmitVersions(hash, matrix, gss), gen);
            Write(outDir, "Aero.Protocol.Matrix.cs", EmitMatrix(hash, matrix), gen);
            Write(outDir, "Aero.Protocol.Patches.cs", EmitPatches(hash, patches), gen);
            Write(outDir, "Aero.Protocol.Gss.Root.cs", EmitGssRoot(hash, gss), gen);
            foreach (var ns in gss.Namespaces)
                Write(outDir, $"Aero.Protocol.Gss.{ns.Name}.cs", EmitGssNamespace(hash, ns), gen);
            Write(outDir, "Aero.Protocol.Gss.Tables.cs", EmitGssTables(hash, gss), gen);
            Write(outDir, "AeroMessageIdAttribute.Protocol.cs", EmitAttributeOverloads(hash, gss), gen);

            return gen;
        }

        // ---------- model building ----------

        static MatrixData BuildMatrix(Result dump)
        {
            var (versions, firstBuild) = OrderVersions(dump.Patches, p => p.MatrixProtocolVersion, dump.MatrixProtocols.Keys);
            var tables = versions.Select(v => dump.MatrixProtocols[v].Messages).ToArray();

            var (names, ordinal) = BuildUnion(tables);
            var ids = FillIds(versions, names, ordinal, v => tables[v]);

            return new MatrixData
            {
                Versions = versions,
                FirstBuild = firstBuild,
                Table = new MessageTable { Names = names, Ids = ids },
            };
        }

        static GssData BuildGss(Result dump)
        {
            var (versions, firstBuild) = OrderVersions(dump.Patches, p => p.GSSProtocolVersion, dump.GssProtocols.Keys);
            var gss = new GssData { Versions = versions, FirstBuild = firstBuild };

            var nsIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            var rootTables = versions.Select(v => dump.GssProtocols[v].Messages).ToArray();
            var (rootNames, rootOrdinal) = BuildUnion(rootTables);
            gss.Root = new MessageTable { Names = rootNames, Ids = FillIds(versions, rootNames, rootOrdinal, v => rootTables[v]) };

            // union of (ns, kind) names in first-appearance order (versions ascending, names sorted within a version)
            var nsTables = new Dictionary<(int Ns, int Kind), (List<string> names, Dictionary<string, int> ordinal)>();
            for (int v = 0; v < versions.Length; v++)
            {
                var proto = dump.GssProtocols[versions[v]];
                if (proto.Children == null) continue;

                foreach (var (nsName, child) in proto.Children.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    bool hasContent = (child.Views?.Count > 0) || (child.Messages?.Count > 0) || (child.Commands?.Count > 0);
                    if (!hasContent) continue;

                    if (!nsIndex.TryGetValue(nsName, out int nsIdx))
                    {
                        gss.Namespaces.Add(new GssNamespace { Name = nsName });
                        nsIdx = nsIndex[nsName] = gss.Namespaces.Count - 1;
                    }

                    UnionInto(nsTables, (nsIdx, View), child.Views);
                    UnionInto(nsTables, (nsIdx, Message), child.Messages);
                    UnionInto(nsTables, (nsIdx, Command), child.Commands);
                }
            }

            foreach (var (key, t) in nsTables)
            {
                int nsIdx = key.Ns, kind = key.Kind;
                string nsName = gss.Namespaces[nsIdx].Name;
                var names = t.names.ToArray();
                gss.Namespaces[nsIdx].Tables[kind] = new MessageTable
                {
                    Names = names,
                    Ids = FillIds(versions, names, t.ordinal, v => KindTable(ChildAt(dump, versions, v, nsName), kind)),
                };
            }

            // route table: (versionIndex, routeId, packed) with packed = ((nsIndex + 2) << 2) | kind
            var routeEntries = new List<int>();
            for (int v = 0; v < versions.Length; v++)
            {
                var routing = dump.GssProtocols[versions[v]].Routing;
                if (routing == null) continue;

                foreach (var (routeName, routeId) in routing.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    int packed;
                    if (routeName == "Generic")
                    {
                        packed = PackRoute(-1, Message);
                    }
                    else if (routeName.Contains("::"))
                    {
                        var prefix = routeName.Substring(0, routeName.IndexOf("::"));
                        packed = PackRoute(nsIndex.TryGetValue(prefix, out int vi) ? vi : -2, View);
                    }
                    else
                    {
                        packed = PackRoute(nsIndex.TryGetValue(routeName, out int ni) ? ni : -2, Message);
                    }

                    if (packed != 0)
                    {
                        routeEntries.Add(v);
                        routeEntries.Add(routeId);
                        routeEntries.Add(packed);
                    }
                }
            }
            gss.RouteEntries = routeEntries.ToArray();

            return gss;
        }

        static List<Result.Patch> BuildPatches(Result dump) => dump.Patches
            .OrderBy(p => p.Environment, StringComparer.Ordinal)
            .ThenBy(p => p.Branch, StringComparer.Ordinal)
            .ThenBy(p => ParsePatchVersion(p.Version))
            .ThenBy(p => p.Version, StringComparer.Ordinal)
            .ToList();

        // Raw protocol version numbers are opaque (we don't know the underlying version hash),
        // so they carry no chronological order. Order versions by the earliest client build
        // (patch) that references each version, so V1 is the chronologically oldest.
        static (ushort[] versions, DateTime[] firstBuild) OrderVersions(
            IReadOnlyCollection<Result.Patch> patches,
            Func<Result.Patch, ushort> versionOf,
            IEnumerable<ushort> allVersions)
        {
            var firstBuild = new Dictionary<ushort, DateTime>();
            foreach (var p in patches)
            {
                var v = versionOf(p);
                if (v == 0) continue;
                if (!firstBuild.TryGetValue(v, out var t) || p.ExeBuildTime < t)
                    firstBuild[v] = p.ExeBuildTime;
            }

            var missing = allVersions.Where(v => !firstBuild.ContainsKey(v)).OrderBy(v => v).ToArray();
            if (missing.Length > 0)
                throw new Exception($"no patch references protocol version(s) {string.Join(", ", missing)}, cannot order versions chronologically");

            var versions = allVersions.OrderBy(v => firstBuild[v]).ThenBy(v => v).ToArray();
            return (versions, versions.Select(v => firstBuild[v]).ToArray());
        }

        // ---------- model helpers ----------

        static int PackRoute(int nsIndex, int kind) => ((nsIndex + 2) << 4) | kind;

        static double ParsePatchVersion(string version) =>
            double.TryParse(version.Split('.')[0], out var d) ? d : double.MaxValue;

        static Result.Namespace ChildAt(Result dump, ushort[] versions, int v, string nsName)
        {
            var proto = dump.GssProtocols[versions[v]];
            return proto.Children != null && proto.Children.TryGetValue(nsName, out var child) ? child : null;
        }

        static Dictionary<string, byte> KindTable(Result.Namespace ns, int kind)
        {
            if (ns == null) return null;
            return kind switch
            {
                View => ns.Views,
                Message => ns.Messages,
                Command => ns.Commands,
                _ => null,
            };
        }

        static (string[] names, Dictionary<string, int> ordinal) BuildUnion(
            IEnumerable<Dictionary<string, byte>> tables)
        {
            var ordinal = new Dictionary<string, int>(StringComparer.Ordinal);
            var names = new List<string>();
            foreach (var table in tables)
            {
                if (table == null) continue;
                foreach (var name in table.Keys.OrderBy(x => x, StringComparer.Ordinal))
                    if (ordinal.TryAdd(name, names.Count)) names.Add(name);
            }
            return (names.ToArray(), ordinal);
        }

        static byte[,] FillIds(
            ushort[] versions,
            string[] names,
            Dictionary<string, int> ordinal,
            Func<int, Dictionary<string, byte>> tableAt)
        {
            var ids = new byte[versions.Length, names.Length];
            for (int v = 0; v < versions.Length; v++)
            {
                var table = tableAt(v);
                if (table == null) continue;
                foreach (var (name, id) in table)
                {
                    if (id == 0)
                        throw new Exception($"message '{name}' has wire id 0 in version {versions[v]}, 0 is reserved as the 'absent' sentinel");
                    ids[v, ordinal[name]] = id;
                }
            }
            return ids;
        }

        static void UnionInto(
            Dictionary<(int Ns, int Kind), (List<string> names, Dictionary<string, int> ordinal)> target,
            (int Ns, int Kind) key,
            Dictionary<string, byte> table)
        {
            if (table == null || table.Count == 0) return;
            if (!target.TryGetValue(key, out var t))
                target[key] = t = (new List<string>(), new Dictionary<string, int>(StringComparer.Ordinal));
            foreach (var name in table.Keys.OrderBy(x => x, StringComparer.Ordinal))
                if (t.ordinal.TryAdd(name, t.names.Count)) t.names.Add(name);
        }

        static string ComputeDumpHash(string dumpsDir)
        {
            var perFile = new List<string>();
            foreach (var file in Directory.GetFiles(dumpsDir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(dumpsDir, file).Replace('\\', '/');
                using var sha = SHA256.Create();
                var relBytes = Encoding.UTF8.GetBytes(rel);
                sha.TransformBlock(relBytes, 0, relBytes.Length, null, 0);
                using (var fs = File.OpenRead(file))
                {
                    var buf = new byte[65536];
                    int n;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                        sha.TransformBlock(buf, 0, n, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                perFile.Add(Convert.ToHexString(sha.Hash));
            }

            using var sha2 = SHA256.Create();
            var joined = Encoding.UTF8.GetBytes(string.Join("\n", perFile));
            sha2.TransformBlock(joined, 0, joined.Length, null, 0);
            sha2.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha2.Hash);
        }

        static void Write(string outDir, string fileName, string content, GenerationResult gen)
        {
            var path = Path.Combine(outDir, fileName);
            var existing = File.Exists(path) ? File.ReadAllText(path) : null;
            if (existing == content)
            {
                gen.UnchangedFiles.Add(fileName);
                return;
            }
            File.WriteAllText(path, content);
            gen.WrittenFiles.Add(fileName);
        }

        // ---------- emission ----------

        static string Header(string hash) =>
 $"""
 // <auto-generated>
 //   Generated by Aero.ProtocolGen from Sift protocol dumps. DO NOT EDIT.
 //   Regenerate with: dotnet run --project Aero.ProtocolGen -- --dumps <dumpsDir> --out <outDir>
 //   Dump content hash: {hash}
 // </auto-generated>
 """
            + "\n";

        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        static string IsoUtc(DateTime t) =>
            "\"" + t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture) + "\"";

        static void EmitEnumMembers(StringBuilder sb, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                sb.AppendLine($"        {names[i]}{(i + 1 < names.Length ? "," : "")}");
        }

        static void EmitByteMatrix(StringBuilder sb, byte[,] ids)
        {
            int rows = ids.GetLength(0), cols = ids.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                sb.Append("            { ");
                for (int c = 0; c < cols; c++)
                    sb.Append($"{ids[r, c]}{(c + 1 < cols ? ", " : "")}");
                sb.Append(" },");
                sb.AppendLine();
            }
        }

        static string EmitVersions(string hash, MatrixData matrix, GssData gss)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            AppendVersionEnum(sb, "MatrixVersion", matrix.Versions);
            AppendVersionEnum(sb, "GssVersion", gss.Versions);
            sb.AppendLine("    public static class ProtocolVersions");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static readonly ushort[] MatrixRaw = {{ {string.Join(", ", matrix.Versions)} }};");
            sb.AppendLine($"        public static readonly string[] MatrixFirstBuild = {{ {string.Join(", ", matrix.FirstBuild.Select(IsoUtc))} }};");
            sb.AppendLine($"        public static readonly ushort[] GssRaw = {{ {string.Join(", ", gss.Versions)} }};");
            sb.AppendLine($"        public static readonly string[] GssFirstBuild = {{ {string.Join(", ", gss.FirstBuild.Select(IsoUtc))} }};");
            sb.AppendLine();
            AppendLookup(sb, "MatrixLookup", "MatrixVersion", matrix.Versions);
            AppendLookup(sb, "GssLookup", "GssVersion", gss.Versions);
            sb.AppendLine("        public static bool TryGetMatrixVersion(ushort raw, out MatrixVersion version) => MatrixLookup.TryGetValue(raw, out version);");
            sb.AppendLine("        public static bool TryGetGssVersion(ushort raw, out GssVersion version) => GssLookup.TryGetValue(raw, out version);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static void AppendVersionEnum(StringBuilder sb, string name, ushort[] versions)
        {
            sb.AppendLine($"    /// Universal protocol versions, V1 is the oldest. Ordered chronologically by the earliest client build (Patches dumps) that used each version; raw version numbers are opaque and not in chronological order.");
            sb.AppendLine($"    public enum {name}");
            sb.AppendLine("    {");
            for (int i = 0; i < versions.Length; i++)
                sb.AppendLine($"        V{i + 1}{(i + 1 < versions.Length ? "," : "")}");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        static void AppendLookup(StringBuilder sb, string field, string enumName, ushort[] versions)
        {
            sb.AppendLine($"        private static readonly Dictionary<ushort, {enumName}> {field} = new({versions.Length})");
            sb.AppendLine("        {");
            for (int i = 0; i < versions.Length; i++)
                sb.AppendLine($"            {{ {versions[i]}, {enumName}.V{i + 1} }},");
            sb.AppendLine("        };");
            sb.AppendLine();
        }

        static string EmitMatrix(string hash, MatrixData matrix)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            sb.AppendLine("    /// Universal Matrix protocol message ids. Values are stable: new messages are appended, existing members never move.");
            sb.AppendLine("    public enum MatrixMessage");
            sb.AppendLine("    {");
            EmitEnumMembers(sb, matrix.Table.Names);
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// Maps universal Matrix message enums to per-version wire ids.");
            sb.AppendLine("    public static class MatrixTables");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const int VersionCount = {matrix.Versions.Length};");
            sb.AppendLine($"        public const int MessageCount = {matrix.Table.Names.Length};");
            sb.AppendLine();
            sb.AppendLine("        // [versionIndex, messageOrdinal] -> wire id, 0 = the message does not exist in that version");
            sb.AppendLine("        private static readonly byte[,] MessageIds =");
            sb.AppendLine("        {");
            EmitByteMatrix(sb, matrix.Table.Ids);
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Dictionary<int, int> IdToOrdinal = new(VersionCount * MessageCount);");
            sb.AppendLine("        static MatrixTables()");
            sb.AppendLine("        {");
            sb.AppendLine("            for (int v = 0; v < VersionCount; v++)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int m = 0; m < MessageCount; m++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    byte id = MessageIds[v, m];");
            sb.AppendLine("                    if (id != 0)");
            sb.AppendLine("                        IdToOrdinal[(v << 8) | id] = m;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static byte GetMessageId(MatrixVersion version, MatrixMessage message) => MessageIds[(int)version, (int)message];");
            sb.AppendLine();
            sb.AppendLine("        /// Returns the ordinal of the message with the given wire id in the given version, or -1 if the id is unknown for that version.");
            sb.AppendLine("        public static int FindMessage(MatrixVersion version, byte messageId)");
            sb.AppendLine("        {");
            sb.AppendLine("            int key = ((int)version << 8) | messageId;");
            sb.AppendLine("            return IdToOrdinal.TryGetValue(key, out int ordinal) ? ordinal : -1;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Gets the wire id for a Matrix message in a version. Returns false if the message is absent in that version.");
            sb.AppendLine("        public static bool TryGetMessageId(MatrixVersion version, MatrixMessage message, out byte messageId)");
            sb.AppendLine("        {");
            sb.AppendLine("            messageId = GetMessageId(version, message);");
            sb.AppendLine("            return messageId != 0;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string EmitGssRoot(string hash, GssData gss)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            sb.AppendLine("    /// Universal GSS protocol message ids for the root (unnamespaced) message table. Values are stable: new messages are appended, existing members never move.");
            sb.AppendLine("    public enum GssMessage");
            sb.AppendLine("    {");
            EmitEnumMembers(sb, gss.Root.Names);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string EmitGssNamespace(string hash, GssNamespace ns)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            for (int kind = 0; kind < 3; kind++)
            {
                var table = ns.Tables[kind];
                if (table == null) continue;
                if (kind > 0 && ns.Tables[kind - 1] != null) sb.AppendLine();
                sb.AppendLine($"    /// Universal GSS {ns.Name} {KindEnumNames[kind].ToLowerInvariant()} ids. Values are stable: new members are appended, existing members never move.");
                sb.AppendLine($"    public enum Gss{ns.Name}{KindEnumNames[kind]}");
                sb.AppendLine("    {");
                EmitEnumMembers(sb, table.Names);
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string EmitGssTables(string hash, GssData gss)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            sb.AppendLine("    /// Maps universal GSS protocol enums to per-version wire ids and routes.");
            sb.AppendLine("    public static class GssTables");
            sb.AppendLine("    {");
            sb.AppendLine("        public static class Ns");
            sb.AppendLine("        {");
            sb.AppendLine("            public const int Root = -1;");
            sb.AppendLine("            public const int Unknown = -2;");
            for (int i = 0; i < gss.Namespaces.Count; i++)
                sb.AppendLine($"            public const int {gss.Namespaces[i].Name} = {i};");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static class Kind");
            sb.AppendLine("        {");
            sb.AppendLine("            public const int View = 0;");
            sb.AppendLine("            public const int Message = 1;");
            sb.AppendLine("            public const int Command = 2;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public const int VersionCount = {gss.Versions.Length};");
            sb.AppendLine($"        public const int NamespaceCount = {gss.Namespaces.Count};");
            sb.AppendLine();
            sb.AppendLine("        // (versionIndex, routeId, packed) triples, packed = ((nsIndex + 2) << 4) | kind");
            sb.AppendLine("        private static readonly int[] RouteEntries =");
            sb.AppendLine("        {");
            for (int i = 0; i < gss.RouteEntries.Length; i += 3)
                sb.AppendLine($"            {gss.RouteEntries[i]}, {gss.RouteEntries[i + 1]}, {gss.RouteEntries[i + 2]},");
            sb.AppendLine("        };");
            sb.AppendLine();

            EmitForwardTable(sb, "RootMessages", gss.Root.Ids);
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                var ns = gss.Namespaces[i];
                for (int kind = 0; kind < 3; kind++)
                {
                    if (ns.Tables[kind] == null) continue;
                    EmitForwardTable(sb, $"{ns.Name}{KindFieldNames[kind]}", ns.Tables[kind].Ids);
                }
            }

            var tableInitializers = new List<string>((gss.Namespaces.Count + 1) * 3);
            tableInitializers.Add("null");
            tableInitializers.Add("RootMessages");
            tableInitializers.Add("null");
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                for (int kind = 0; kind < 3; kind++)
                    tableInitializers.Add(gss.Namespaces[i].Tables[kind] != null
                        ? $"{gss.Namespaces[i].Name}{KindFieldNames[kind]}"
                        : "null");
            }

            sb.AppendLine("        private static readonly List<byte[,]> Tables = new List<byte[,]>((NamespaceCount + 1) * 3)");
            sb.AppendLine("        {");
            for (int i = 0; i < tableInitializers.Count; i += 6)
            {
                var end = Math.Min(i + 6, tableInitializers.Count);
                var fields = new List<string>(end - i);
                for (int j = i; j < end; j++)
                    fields.Add(tableInitializers[j]);
                sb.AppendLine($"            {string.Join(", ", fields)}{(end < tableInitializers.Count ? "," : "")}");
            }
            sb.AppendLine("        };");
            sb.AppendLine("        private static readonly short[][] NsRouteIds = new short[VersionCount][];");
            sb.AppendLine("        private static readonly Dictionary<long, int> IdToOrdinal = new(24000);");
            sb.AppendLine("        private static readonly Dictionary<long, int> RouteToView = new(4096);");
            sb.AppendLine("        private static readonly Dictionary<long, int> RouteToNamespace = new(4096);");
            sb.AppendLine();
            sb.AppendLine("        static GssTables()");
            sb.AppendLine("        {");
            sb.AppendLine("            Tables[(Ns.Root + 1) * 3 + Kind.Message] = RootMessages;");
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                var ns = gss.Namespaces[i];
                for (int kind = 0; kind < 3; kind++)
                {
                    if (ns.Tables[kind] == null) continue;
                    sb.AppendLine($"            Tables[(Ns.{ns.Name} + 1) * 3 + Kind.{KindEnumNames[kind]}] = {ns.Name}{KindFieldNames[kind]};");
                }
            }
            sb.AppendLine();
            sb.AppendLine("            for (int v = 0; v < VersionCount; v++)");
            sb.AppendLine("            {");
            sb.AppendLine("                NsRouteIds[v] = new short[NamespaceCount + 1];");
            sb.AppendLine("                for (int i = 0; i < NsRouteIds[v].Length; i++)");
            sb.AppendLine("                    NsRouteIds[v][i] = -1;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            for (int i = 0; i < RouteEntries.Length; i += 3)");
            sb.AppendLine("            {");
            sb.AppendLine("                int v = RouteEntries[i];");
            sb.AppendLine("                int routeId = RouteEntries[i + 1];");
            sb.AppendLine("                int packed = RouteEntries[i + 2];");
            sb.AppendLine("                int ns = (packed >> 4) - 2;");
            sb.AppendLine("                int kind = packed & 15;");
            sb.AppendLine("                if (ns == Ns.Unknown)");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                if (kind == Kind.View)");
            sb.AppendLine("                {");
            sb.AppendLine("                    byte[,] views = Tables[(ns + 1) * 3 + Kind.View];");
            sb.AppendLine("                    if (views != null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        for (int m = 0; m < views.GetLength(1); m++)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            if (views[v, m] == routeId)");
            sb.AppendLine("                            {");
            sb.AppendLine("                                RouteToView[((long)v << 8) | ((long)routeId & 0xFF)] = (ns << 16) | m;");
            sb.AppendLine("                                break;");
            sb.AppendLine("                            }");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                    RouteToNamespace[((long)v << 16) | (((long)routeId & 0xFF) << 8) | (long)Kind.Message] = ns;");
            sb.AppendLine("                    RouteToNamespace[((long)v << 16) | (((long)routeId & 0xFF) << 8) | (long)Kind.Command] = ns;");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (ns == Ns.Root)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        NsRouteIds[v][0] = (short)routeId;");
            sb.AppendLine("                        RouteToNamespace[((long)v << 16) | (((long)routeId & 0xFF) << 8) | (long)Kind.Message] = ns;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine("                        NsRouteIds[v][ns + 1] = (short)routeId;");
            sb.AppendLine("                        RouteToNamespace[((long)v << 16) | (((long)routeId & 0xFF) << 8) | (long)Kind.Message] = ns;");
            sb.AppendLine("                        RouteToNamespace[((long)v << 16) | (((long)routeId & 0xFF) << 8) | (long)Kind.Command] = ns;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            for (int i = 0; i < RouteEntries.Length; i += 3)");
            sb.AppendLine("            {");
            sb.AppendLine("                int v = RouteEntries[i];");
            sb.AppendLine("                int routeId = RouteEntries[i + 1];");
            sb.AppendLine("                int packed = RouteEntries[i + 2];");
            sb.AppendLine("                int ns = (packed >> 4) - 2;");
            sb.AppendLine("                int kind = packed & 15;");
            sb.AppendLine("                if (ns == Ns.Unknown)");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                // Messages and commands are addressed by the namespace route AND by the view routes");
            sb.AppendLine("                // of their namespace, so index the message/command tables under both route kinds.");
            sb.AppendLine("                int firstKind = kind == Kind.View ? Kind.Message : kind;");
            sb.AppendLine("                int kindCount = ns == Ns.Root ? 1 : 2;");
            sb.AppendLine("                for (int k = 0; k < kindCount; k++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    int tk = k == 0 ? firstKind : Kind.Command;");
            sb.AppendLine("                    byte[,] ids = Tables[(ns + 1) * 3 + tk];");
            sb.AppendLine("                    if (ids == null)");
            sb.AppendLine("                        continue;");
            sb.AppendLine("                    for (int m = 0; m < ids.GetLength(1); m++)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        byte id = ids[v, m];");
            sb.AppendLine("                        if (id != 0)");
            sb.AppendLine("                            IdToOrdinal[((long)v << 32) | ((long)tk << 24) | (((long)routeId & 0xFF) << 8) | (long)id] = m;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Namespace typecode for a version (root is always 0), 255 = the namespace is not routed in that version.");
            sb.AppendLine("        public static byte GetNamespaceTypecode(GssVersion version, int nsIndex)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (nsIndex == Ns.Root) return 0;");
            sb.AppendLine("            if (nsIndex < 0 || nsIndex >= NamespaceCount) return 255;");
            sb.AppendLine("            return (byte)NsRouteIds[(int)version][nsIndex + 1];");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Wire id of a universal message/command/view in a version, 0 = it does not exist in that version.");
            sb.AppendLine("        public static byte GetMessageId(GssVersion version, int nsIndex, int kind, int ordinal)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (nsIndex < Ns.Root || nsIndex >= NamespaceCount) return 0;");
            sb.AppendLine("            byte[,] ids = Tables[(nsIndex + 1) * 3 + kind];");
            sb.AppendLine("            if (ids == null) return 0;");
            sb.AppendLine("            if (ordinal < 0 || ordinal >= ids.GetLength(1)) return 0;");
            sb.AppendLine("            return ids[(int)version, ordinal];");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Namespace carried by (typecode, kind) in a version, Ns.Unknown = the typecode is not known for that version.");
            sb.AppendLine("        /// The typecode may be a namespace route or a view route of the namespace.");
            sb.AppendLine("        public static int FindNamespace(GssVersion version, byte typecode, int kind)");
            sb.AppendLine("        {");
            sb.AppendLine("            long key = ((long)(int)version << 16) | (((long)typecode & 0xFF) << 8) | (long)kind;");
            sb.AppendLine("            return RouteToNamespace.TryGetValue(key, out int nsIndex) ? nsIndex : Ns.Unknown;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Ordinal of the message/command carried by (typecode, messageId) in a version, -1 = unknown id for that version.");
            sb.AppendLine("        /// The typecode may be a namespace route or a view route of the namespace.");
            sb.AppendLine("        public static int FindMessage(GssVersion version, byte typecode, int kind, byte messageId)");
            sb.AppendLine("        {");
            sb.AppendLine("            long key = ((long)(int)version << 32) | ((long)kind << 24) | (((long)typecode & 0xFF) << 8) | (long)messageId;");
            sb.AppendLine("            return IdToOrdinal.TryGetValue(key, out int ordinal) ? ordinal : -1;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Ordinal of the view carried by a typecode in a version, -1 = not a view typecode in that version.");
            sb.AppendLine("        public static int FindView(GssVersion version, byte typecode)");
            sb.AppendLine("        {");
            sb.AppendLine("            long key = ((long)(int)version << 8) | ((long)typecode & 0xFF);");
            sb.AppendLine("            return RouteToView.TryGetValue(key, out int packed) ? (packed & 0xFFFF) : -1;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Namespace and ordinal of the view carried by a typecode in a version, false = not a view typecode in that version.");
            sb.AppendLine("        public static bool TryFindView(GssVersion version, byte typecode, out int nsIndex, out int ordinal)");
            sb.AppendLine("        {");
            sb.AppendLine("            long key = ((long)(int)version << 8) | ((long)typecode & 0xFF);");
            sb.AppendLine("            if (RouteToView.TryGetValue(key, out int packed))");
            sb.AppendLine("            {");
            sb.AppendLine("                nsIndex = packed >> 16;");
            sb.AppendLine("                ordinal = packed & 0xFFFF;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            nsIndex = Ns.Unknown;");
            sb.AppendLine("            ordinal = -1;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Maps a GSS message/command enum type name to its namespace and kind.");
            sb.AppendLine("        public static bool TryGetMessageEnumInfo(string messageEnum, out int nsIndex, out int kind)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (messageEnum)");
            sb.AppendLine("            {");
            sb.AppendLine("                case \"GssMessage\":");
            sb.AppendLine("                    nsIndex = Ns.Root;");
            sb.AppendLine("                    kind = Kind.Message;");
            sb.AppendLine("                    return true;");
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                var ns = gss.Namespaces[i];
                if (ns.Tables[Message] != null)
                {
                    sb.AppendLine($"                case \"Gss{ns.Name}Message\":");
                    sb.AppendLine($"                    nsIndex = Ns.{ns.Name};");
                    sb.AppendLine("                    kind = Kind.Message;");
                    sb.AppendLine("                    return true;");
                }
                if (ns.Tables[Command] != null)
                {
                    sb.AppendLine($"                case \"Gss{ns.Name}Command\":");
                    sb.AppendLine($"                    nsIndex = Ns.{ns.Name};");
                    sb.AppendLine("                    kind = Kind.Command;");
                    sb.AppendLine("                    return true;");
                }
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    nsIndex = Ns.Unknown;");
            sb.AppendLine("                    kind = 0;");
            sb.AppendLine("                    return false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Maps a GSS protocol enum type name to its namespace and kind, including views.");
            sb.AppendLine("        public static bool TryGetProtocolEnumInfo(string protocolEnum, out int nsIndex, out int kind)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (protocolEnum)");
            sb.AppendLine("            {");
            sb.AppendLine("                case \"GssMessage\":");
            sb.AppendLine("                    nsIndex = Ns.Root;");
            sb.AppendLine("                    kind = Kind.Message;");
            sb.AppendLine("                    return true;");
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                var ns = gss.Namespaces[i];
                if (ns.Tables[View] != null)
                {
                    sb.AppendLine($"                case \"Gss{ns.Name}View\":");
                    sb.AppendLine($"                    nsIndex = Ns.{ns.Name};");
                    sb.AppendLine("                    kind = Kind.View;");
                    sb.AppendLine("                    return true;");
                }
                if (ns.Tables[Message] != null)
                {
                    sb.AppendLine($"                case \"Gss{ns.Name}Message\":");
                    sb.AppendLine($"                    nsIndex = Ns.{ns.Name};");
                    sb.AppendLine("                    kind = Kind.Message;");
                    sb.AppendLine("                    return true;");
                }
                if (ns.Tables[Command] != null)
                {
                    sb.AppendLine($"                case \"Gss{ns.Name}Command\":");
                    sb.AppendLine($"                    nsIndex = Ns.{ns.Name};");
                    sb.AppendLine("                    kind = Kind.Command;");
                    sb.AppendLine("                    return true;");
                }
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    nsIndex = Ns.Unknown;");
            sb.AppendLine("                    kind = 0;");
            sb.AppendLine("                    return false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// Gets the wire ids for a GSS protocol enum in a version.");
            sb.AppendLine("        /// For views, typecode is the view/controller typecode and messageId is 0.");
            sb.AppendLine("        /// For messages/commands, typecode is the namespace route's typecode; use the (view, message)");
            sb.AppendLine("        /// overload for a message sent through a view route.");
            sb.AppendLine("        public static bool TryGetWireIds<T>(GssVersion version, T protocolEnum, out byte typecode, out byte messageId)");
            sb.AppendLine("            where T : struct");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!(protocolEnum is System.Enum))");
            sb.AppendLine("            {");
            sb.AppendLine("                typecode = 0;");
            sb.AppendLine("                messageId = 0;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (!TryGetProtocolEnumInfo(typeof(T).Name, out int nsIndex, out int kind))");
            sb.AppendLine("            {");
            sb.AppendLine("                typecode = 0;");
            sb.AppendLine("                messageId = 0;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            int ordinal = System.Convert.ToInt32(protocolEnum);");
            sb.AppendLine();
            sb.AppendLine("            if (kind == Kind.View)");
            sb.AppendLine("            {");
            sb.AppendLine("                typecode = GetMessageId(version, nsIndex, kind, ordinal);");
            sb.AppendLine("                messageId = 0;");
            sb.AppendLine("                return typecode != 0;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            typecode = GetNamespaceTypecode(version, nsIndex);");
            sb.AppendLine("            messageId = GetMessageId(version, nsIndex, kind, ordinal);");
            sb.AppendLine("            return typecode != 255 && messageId != 0;");
            sb.AppendLine("        }");
            for (int i = 0; i < gss.Namespaces.Count; i++)
            {
                var ns = gss.Namespaces[i];
                if (ns.Tables[View] == null) continue;
                if (ns.Tables[Message] != null)
                    EmitViewWireIdOverload(sb, ns.Name, "Message");
                if (ns.Tables[Command] != null)
                    EmitViewWireIdOverload(sb, ns.Name, "Command");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static void EmitViewWireIdOverload(StringBuilder sb, string nsName, string kindName)
        {
            string paramName = kindName.ToLowerInvariant();
            sb.AppendLine($"        /// Gets the wire ids of a GSS {paramName} sent through a view route in a version.");
            sb.AppendLine("        /// typecode is the view route's typecode, messageId is the " + paramName + "'s wire id, false = the view or " + paramName + " does not exist in that version.");
            sb.AppendLine($"        public static bool TryGetWireIds(GssVersion version, Gss{nsName}View view, Gss{nsName}{kindName} {paramName}, out byte typecode, out byte messageId)");
            sb.AppendLine("        {");
            sb.AppendLine($"            typecode = GetMessageId(version, Ns.{nsName}, Kind.View, (int)view);");
            sb.AppendLine($"            messageId = GetMessageId(version, Ns.{nsName}, Kind.{kindName}, (int){paramName});");
            sb.AppendLine("            return typecode != 0 && messageId != 0;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        static void EmitForwardTable(StringBuilder sb, string fieldName, byte[,] ids)
        {
            sb.AppendLine($"        // {fieldName}: [versionIndex, ordinal] -> wire id, 0 = absent");
            sb.AppendLine($"        private static readonly byte[,] {fieldName} =");
            sb.AppendLine("        {");
            EmitByteMatrix(sb, ids);
            sb.AppendLine("        };");
            sb.AppendLine();
        }

        static string EmitPatches(string hash, List<Result.Patch> patches)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Protocol");
            sb.AppendLine("{");
            sb.AppendLine("    /// Client build -> protocol version pairs from the Sift Patches dumps.");
            sb.AppendLine("    public static class Patches");
            sb.AppendLine("    {");
            sb.AppendLine("        public struct PatchInfo");
            sb.AppendLine("        {");
            sb.AppendLine("            public string Environment;");
            sb.AppendLine("            public string Branch;");
            sb.AppendLine("            public string Version;");
            sb.AppendLine("            public string ExeBuildTime;");
            sb.AppendLine("            public ushort MatrixProtocolVersion;");
            sb.AppendLine("            public ushort GssProtocolVersion;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static readonly PatchInfo[] All = new PatchInfo[{patches.Count}]");
            sb.AppendLine("        {");
            foreach (var p in patches)
            {
                sb.AppendLine($"            new PatchInfo {{ Environment = \"{Escape(p.Environment)}\", Branch = \"{Escape(p.Branch)}\", Version = \"{Escape(p.Version)}\", ExeBuildTime = \"{p.ExeBuildTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}Z\", MatrixProtocolVersion = {p.MatrixProtocolVersion}, GssProtocolVersion = {p.GSSProtocolVersion} }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Dictionary<string, PatchInfo> Lookup = new(All.Length, StringComparer.Ordinal);");
            sb.AppendLine("        static Patches()");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (var p in All)");
            sb.AppendLine("            {");
            sb.AppendLine("                string key = $\"{p.Environment}\\0{p.Branch}\\0{p.Version}\";");
            sb.AppendLine("                if (!Lookup.ContainsKey(key))");
            sb.AppendLine("                    Lookup.Add(key, p);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGet(string environment, string branch, string version, out PatchInfo patch)");
            sb.AppendLine("            => Lookup.TryGetValue($\"{environment}\\0{branch}\\0{version}\", out patch);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string EmitAttributeOverloads(string hash, GssData gss)
        {
            var sb = new StringBuilder();
            sb.Append(Header(hash));
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace Aero.Gen.Attributes");
            sb.AppendLine("{");
            sb.AppendLine("    using Aero.Protocol;");
            sb.AppendLine();
            sb.AppendLine("    public partial class AeroMessageIdAttribute");
            sb.AppendLine("    {");
            AppendOverload(sb, "MatrixMessage", "MatrixVersion");
            AppendOverload(sb, "GssMessage", "GssVersion");
            foreach (var ns in gss.Namespaces)
            {
                if (ns.Tables[View] != null) AppendOverload(sb, $"Gss{ns.Name}View", "GssVersion");
                if (ns.Tables[Message] != null) AppendOverload(sb, $"Gss{ns.Name}Message", "GssVersion");
                if (ns.Tables[Command] != null) AppendOverload(sb, $"Gss{ns.Name}Command", "GssVersion");
                if (ns.Tables[View] != null && ns.Tables[Message] != null)
                    AppendViewOverload(sb, $"Gss{ns.Name}Message", $"Gss{ns.Name}View", "GssVersion");
                if (ns.Tables[View] != null && ns.Tables[Command] != null)
                    AppendViewOverload(sb, $"Gss{ns.Name}Command", $"Gss{ns.Name}View", "GssVersion");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static void AppendViewOverload(StringBuilder sb, string msgEnum, string viewEnum, string verEnum)
        {
            sb.AppendLine($"        public AeroMessageIdAttribute(MsgType typ, MsgSrc src, {msgEnum} msg, {viewEnum} view, {verEnum} from = ({verEnum})0, {verEnum} to = ({verEnum})(-1))");
            sb.AppendLine("            : this()");
            sb.AppendLine("        {");
            sb.AppendLine("            Typ = typ;");
            sb.AppendLine("            Src = src;");
            sb.AppendLine($"            MessageEnum = nameof({msgEnum});");
            sb.AppendLine("            MessageOrdinal = (int)msg;");
            sb.AppendLine("            MessageName = msg.ToString();");
            sb.AppendLine($"            ViewEnum = nameof({viewEnum});");
            sb.AppendLine("            ViewOrdinal = (int)view;");
            sb.AppendLine("            ViewName = view.ToString();");
            sb.AppendLine($"            VersionEnum = nameof({verEnum});");
            sb.AppendLine("            VersionFrom = (int)from;");
            sb.AppendLine("            VersionTo = (int)to;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        static void AppendOverload(StringBuilder sb, string msgEnum, string verEnum)
        {
            sb.AppendLine($"        public AeroMessageIdAttribute(MsgType typ, MsgSrc src, {msgEnum} msg, {verEnum} from = ({verEnum})0, {verEnum} to = ({verEnum})(-1))");
            sb.AppendLine("            : this()");
            sb.AppendLine("        {");
            sb.AppendLine("            Typ = typ;");
            sb.AppendLine("            Src = src;");
            sb.AppendLine($"            MessageEnum = nameof({msgEnum});");
            sb.AppendLine("            MessageOrdinal = (int)msg;");
            sb.AppendLine("            MessageName = msg.ToString();");
            sb.AppendLine($"            VersionEnum = nameof({verEnum});");
            sb.AppendLine("            VersionFrom = (int)from;");
            sb.AppendLine("            VersionTo = (int)to;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
}
