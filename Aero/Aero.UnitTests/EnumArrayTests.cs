using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public enum EnumArrayByte : byte
    {
        Low  = 0x05,
        High = 0xFF
    }

    public enum EnumArrayShort : short
    {
        Negative = -300,
        Positive = 300
    }

    [Aero]
    public partial class EnumArrayTest
    {
        [AeroArray(2)]
        public EnumArrayByte[] Fixed;

        [AeroArray(typeof(byte))]
        public EnumArrayShort[] Prefixed;

        public byte Trailer;
    }

    public class EnumArrayTests
    {
        [Test]
        public void EnumArraysAreSerialized()
        {
            var msg = new EnumArrayTest
            {
                Fixed    = new[] {EnumArrayByte.High, EnumArrayByte.Low},
                Prefixed = new[] {EnumArrayShort.Positive, EnumArrayShort.Negative, EnumArrayShort.Positive},
                Trailer  = 0xAB
            };

            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(buffer.Length, Is.EqualTo(2 + 1 + 3 * 2 + 1));
            Assert.That(msg.Pack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(buffer, Is.EqualTo(new byte[] {0xFF, 0x05, 0x03, 0x2C, 0x01, 0xD4, 0xFE, 0x2C, 0x01, 0xAB}));

            var read = new EnumArrayTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.Fixed, Is.EqualTo(msg.Fixed));
            Assert.That(read.Prefixed, Is.EqualTo(msg.Prefixed));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }
    }
}
