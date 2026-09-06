using System;
using System.IO;
using Aero.Protocol;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public class ProtocolGenTests
    {
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
