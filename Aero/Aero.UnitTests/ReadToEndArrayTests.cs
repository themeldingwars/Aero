using System;
using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [AeroBlock]
    public struct RoundTripRow
    {
        public byte Type;

        [AeroIf(nameof(Type), 1)]
        public uint Value;
    }

    // The shape of CombatLogMessage
    [Aero]
    public partial class ReadToEndRoundTripTest
    {
        public ushort Bytes;

        [AeroArray(-4)]
        public RoundTripRow[] Rows;
    }

    // Reads nothing, the condition is never true
    [AeroBlock]
    public struct ReadToEndEmptyElement
    {
        public byte Never => 0;

        [AeroIf(nameof(Never), 1)]
        public byte Value;
    }

    [Aero]
    public partial class ReadToEndEmptyElementTest
    {
        [AeroArray(-4)]
        public ReadToEndEmptyElement[] Elements;
    }

    public class ReadToEndArrayTests
    {
        private static byte[] Pack(Gen.IAero msg)
        {
            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(buffer.Length), "Pack length doesn't match GetPackedSize");
            return buffer;
        }

        [Test]
        public void ReadToEndArrayPacksWhatWasRead()
        {
            var data = new byte[] {0x06, 0x00, 0x01, 0x0A, 0x00, 0x00, 0x00, 0x02};

            var msg = new ReadToEndRoundTripTest();
            Assert.That(msg.Unpack(data), Is.EqualTo(data.Length));
            Assert.That(msg.Rows.Length, Is.EqualTo(2));
            Assert.That(msg.GetRowsCount, Is.EqualTo(2));
            Assert.That(Pack(msg), Is.EqualTo(data));
        }

        [Test]
        public void ReadToEndArrayGrowsPastItsInitialSize()
        {
            var data = Enumerable.Repeat(new byte[] {0x01, 0x07, 0x00, 0x00, 0x00}, 9).SelectMany(x => x).Prepend((byte)0).Prepend((byte)0).ToArray();

            var msg = new ReadToEndRoundTripTest();
            Assert.That(msg.Unpack(data), Is.EqualTo(data.Length));
            Assert.That(msg.Rows.Length, Is.EqualTo(9));
            Assert.That(msg.Rows.All(x => x.Value == 7));
            Assert.That(Pack(msg), Is.EqualTo(data));
        }

        [Test]
        public void EmptyReadToEndArray()
        {
            var data = new byte[] {0x00, 0x00};

            var msg = new ReadToEndRoundTripTest();
            Assert.That(msg.Unpack(data), Is.EqualTo(data.Length));
            Assert.That(msg.Rows, Is.Empty);
            Assert.That(Pack(msg), Is.EqualTo(data));
        }

        [Test]
        public void ElementThatReadsNothingIsAnError()
        {
            Action unpack = () => new ReadToEndEmptyElementTest().Unpack(new byte[] {0x00});
            Assert.Throws<System.IO.InvalidDataException>(unpack);
        }
    }
}
