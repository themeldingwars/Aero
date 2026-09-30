using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    // The bit set at the end of NPCSetInteractionType: a ushort, then ((value >> 6) + 1) * 8 bytes
    [Aero]
    public partial class ComputedBlobLengthTest
    {
        public ushort BitsHeader;

        public int BitsLength => ((BitsHeader >> 6) + 1) * 8;

        [AeroBlob(nameof(BitsLength))]
        public byte[] Bits;

        public byte Trailer;
    }

    public class ComputedLengthTests
    {
        [TestCase((ushort)0, 8)]
        [TestCase((ushort)63, 8)]
        [TestCase((ushort)64, 16)]
        [TestCase((ushort)200, 32)]
        public void BlobLengthFromProperty(ushort header, int length)
        {
            var msg = new ComputedBlobLengthTest
            {
                BitsHeader = header,
                Bits       = Enumerable.Range(1, length).Select(x => (byte)x).ToArray(),
                Trailer    = 0xAB
            };

            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(2 + length + 1));

            var read = new ComputedBlobLengthTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.Bits, Is.EqualTo(msg.Bits));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }

        [Test]
        public void PropertyRefIsNotReportedAsDeclaredAfterTheBlob()
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace ComputedLengthTest
{
    [Aero]
    public partial class Test
    {
        public ushort BitsHeader;

        public int BitsLength => ((BitsHeader >> 6) + 1) * 8;

        [AeroBlob(nameof(BitsLength))]
        public byte[] Bits;

        [AeroBlob(nameof(LaterLength))]
        public byte[] Other;

        public byte LaterLength;
    }
}");

            Assert.That(result.CompileErrors, Is.Empty);
            Assert.That(result.GeneratorDiagnostics.Where(x => x.Id == "Aero14").Select(x => x.GetMessage()).Distinct(),
                Is.EqualTo(new[] {"Field 'Other' references 'LaterLength' which is declared after the blob, the blob will read as length 0"}));
        }
    }
}
