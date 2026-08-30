using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aero.Protocol;
using Aero.ProtocolGen;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public class ProtocolGenTests
    {
        [Test]
        public void Generate_IsDeterministicAndMatchesCheckedInProtocolFacts()
        {
            var dumpsDir = FindDumpsDir();
            if (dumpsDir == null)
                Assert.Ignore("Sift protocol dumps are not available");

            var out1 = Path.Combine(Path.GetTempPath(), "AeroProtocolGenTest1_" + Guid.NewGuid().ToString("N"));
            var out2 = Path.Combine(Path.GetTempPath(), "AeroProtocolGenTest2_" + Guid.NewGuid().ToString("N"));
            try
            {
                var result1 = Aero.ProtocolGen.ProtocolGen.Generate(dumpsDir, out1);
                var result2 = Aero.ProtocolGen.ProtocolGen.Generate(dumpsDir, out2);

                Assert.AreEqual("8AF72793FBDA3729E1FD05FEFBF454E8BCA3BD2222779E37E6A7C2A96F5BA090", result1.DumpHash);
                Assert.AreEqual(result1.DumpHash, result2.DumpHash);
                Assert.AreEqual(26, result1.MatrixVersionCount);
                Assert.AreEqual(58, result1.MatrixMessageCount);
                Assert.AreEqual(67, result1.GssVersionCount);
                Assert.AreEqual(15, result1.GssNamespaceCount);
                Assert.AreEqual(536, result1.GssMessageCount);
                Assert.AreEqual(206, result1.PatchCount);

                var files1 = new DirectoryInfo(out1).GetFiles("*.cs", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(out1, f.FullName)).OrderBy(f => f, StringComparer.Ordinal).ToList();
                var files2 = new DirectoryInfo(out2).GetFiles("*.cs", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(out2, f.FullName)).OrderBy(f => f, StringComparer.Ordinal).ToList();
                CollectionAssert.AreEqual(files1, files2);
                foreach (var file in files1)
                    Assert.AreEqual(File.ReadAllText(Path.Combine(out1, file)), File.ReadAllText(Path.Combine(out2, file)));
            }
            finally
            {
                Directory.Delete(out1, true);
                Directory.Delete(out2, true);
            }
        }

        [Test]
        public void VersionEnums_AreInChronologicalFirstBuildOrder()
        {
            AssertVersionsChronological(ProtocolVersions.MatrixRaw, ProtocolVersions.MatrixFirstBuild);
            AssertVersionsChronological(ProtocolVersions.GssRaw, ProtocolVersions.GssFirstBuild);
        }

        static void AssertVersionsChronological(ushort[] raw, string[] firstBuild)
        {
            Assert.AreEqual(raw.Length, firstBuild.Length);
            for (int i = 1; i < firstBuild.Length; i++)
                Assert.LessOrEqual(DateTime.Parse(firstBuild[i - 1]), DateTime.Parse(firstBuild[i]),
                    $"version {i + 1} first build is earlier than version {i}'s, versions are not in chronological order");
        }

        static string FindDumpsDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Aero.Gen", "Lib", "Sift", "Dumps");
                if (Directory.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }

            return null;
        }
    }
}
