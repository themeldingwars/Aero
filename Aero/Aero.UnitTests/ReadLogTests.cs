using System.Linq;
using Aero.Gen;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public class ReadLogTests
    {
        private const string Source = @"
using Aero.Gen.Attributes;

namespace ReadLogTest
{
    [AeroBlock]
    public struct Entry
    {
        public byte Id;

        [AeroString(typeof(byte))]
        public string Name;
    }

    [Aero]
    public partial class Layout
    {
        public uint A;

        [AeroArray(typeof(byte))]
        public uint[] B;

        public uint C;

        [AeroString(typeof(byte))]
        public string D;

        [AeroBlob(typeof(ushort))]
        public byte[] E;

        [AeroArray(typeof(byte), Chunked = true)]
        public Entry[] F;

        public byte G;
    }
}";

        private static readonly byte[] Data =
        {
            0x01, 0x00, 0x00, 0x00,                         // A        0
            0x02,                                           // B count  4
            0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, // B        5, 9
            0x04, 0x00, 0x00, 0x00,                         // C        13
            0x02, 0x68, 0x69,                               // D        17
            0x03, 0x00, 0xAA, 0xBB, 0xCC,                   // E        20
            0x01,                                           // F count  25
            0x07, 0x01, 0x78,                               // F[0]     26
            0x08                                            // G        29
        };

        private static AeroReadLog[] ReadLogs()
        {
            var result = GeneratorHarness.Run(Source, diagLogging: true);
            Assert.That(result.CompileErrors, Is.Empty);

            var msg = (IAero)result.Create("ReadLogTest.Layout");
            Assert.That(msg.Unpack(Data), Is.EqualTo(Data.Length));

            return msg.GetDiagReadLogs().ToArray();
        }

        private static AeroReadLog Entry(AeroReadLog[] logs, string name, AeroReadLog.LogEntryType type = AeroReadLog.LogEntryType.Field,
                                         string parent = null) =>
            logs.Single(x => x.ParentName == parent && x.Name == name && x.EntryType == type);

        [Test]
        public void FieldsAfterACountStartAfterIt()
        {
            var logs = ReadLogs();

            Assert.That(Entry(logs, "0", parent: "B").Offset, Is.EqualTo(5));
            Assert.That(Entry(logs, "0", parent: "B").Length, Is.EqualTo(4));
            Assert.That(Entry(logs, "C").Offset, Is.EqualTo(13));
            Assert.That(Entry(logs, "C").Length, Is.EqualTo(4));
            Assert.That(Entry(logs, "D").Offset, Is.EqualTo(17));
            Assert.That(Entry(logs, "D").Length, Is.EqualTo(3));
            Assert.That(Entry(logs, "E").Offset, Is.EqualTo(20));
            Assert.That(Entry(logs, "E").Length, Is.EqualTo(5));
            Assert.That(Entry(logs, "Id", parent: "F[0]").Offset, Is.EqualTo(26));
            Assert.That(Entry(logs, "Id", parent: "F[0]").Length, Is.EqualTo(1));
            Assert.That(Entry(logs, "G").Offset, Is.EqualTo(29));
        }

        [Test]
        public void CountsAreLogged()
        {
            var logs = ReadLogs();

            var bCount = Entry(logs, "B", AeroReadLog.LogEntryType.Length);
            Assert.That(bCount.Offset, Is.EqualTo(4));
            Assert.That(bCount.Length, Is.EqualTo(1));

            var fCount = Entry(logs, "F", AeroReadLog.LogEntryType.Length);
            Assert.That(fCount.Offset, Is.EqualTo(25));
            Assert.That(fCount.Length, Is.EqualTo(1));
        }

        private static void AssertTiles(System.Collections.Generic.IEnumerable<AeroReadLog> logs, int length)
        {
            var entries = logs.Where(x => x.EntryType is AeroReadLog.LogEntryType.Field or AeroReadLog.LogEntryType.Length)
                              .OrderBy(x => x.Offset)
                              .ToArray();

            var offset = 0;
            foreach (var entry in entries) {
                Assert.That(entry.Offset, Is.EqualTo(offset), $"{entry}");
                offset += entry.Length;
            }

            Assert.That(offset, Is.EqualTo(length));
        }

        [Test]
        public void EntriesTileThePayload()
        {
            AssertTiles(ReadLogs(), Data.Length);
        }

        private const string ViewSource = @"
using Aero.Gen;
using Aero.Gen.Attributes;

namespace ReadLogTest
{
    [Aero(AeroGenTypes.View)]
    public partial class TestView
    {
        private uint A;

        [AeroArray(typeof(byte))]
        private ushort[] B;

        [AeroNullable]
        private byte C;

        private float D;
    }
}";

        [Test]
        public void ViewEntriesTileThePayload()
        {
            var result = GeneratorHarness.Run(ViewSource, diagLogging: true);
            Assert.That(result.CompileErrors, Is.Empty);

            // Keyframe: nullable bitfield, A, B, C, D
            var keyframe = new byte[] {0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x05, 0x00, 0x06, 0x00, 0x07, 0x00, 0x00, 0x80, 0x40};
            var view     = (IAeroViewInterface)result.Create("ReadLogTest.TestView");
            Assert.That(view.Unpack(keyframe), Is.EqualTo(keyframe.Length));
            AssertTiles(view.GetDiagReadLogs(), keyframe.Length);

            // Update: field index, value pairs
            var update = new byte[] {0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x02, 0x05, 0x00, 0x06, 0x00, 0x03, 0x00, 0x00, 0x80, 0x40};
            view.ClearReadLogs();
            Assert.That(view.UnpackChanges(update), Is.EqualTo(update.Length));
            AssertTiles(view.GetDiagReadLogs(), update.Length);
        }
    }
}
