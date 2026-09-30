using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public enum BigEndianEnum : ushort
    {
        Value = 0x1234
    }

    [Aero]
    public partial class BigEndianTest
    {
        [AeroBigEndian] public short  Short;
        [AeroBigEndian] public ushort UShort;
        [AeroBigEndian] public int    Int;
        [AeroBigEndian] public uint   UInt;
        [AeroBigEndian] public long   Long;
        [AeroBigEndian] public ulong  ULong;

        [AeroBigEndian] public BigEndianEnum Enum;

        [AeroBigEndian]
        [AeroArray(typeof(byte))]
        public ushort[] Array;

        [AeroBigEndian]
        [AeroArray(2)]
        public BigEndianEnum[] EnumArray;

        public ushort LittleEndian;
    }

    public class BigEndianTests
    {
        private static readonly byte[] Data =
        {
            0xFF, 0xFE,
            0x12, 0x34,
            0xFF, 0xFF, 0xFF, 0xFD,
            0x12, 0x34, 0x56, 0x78,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFC,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x12, 0x34,
            0x02, 0x00, 0x01, 0xAB, 0xCD,
            0x12, 0x34, 0x12, 0x34,
            0x34, 0x12
        };

        [Test]
        public void Unpack()
        {
            var msg = new BigEndianTest();
            Assert.That(msg.Unpack(Data), Is.EqualTo(Data.Length));

            Assert.That(msg.Short, Is.EqualTo(-2));
            Assert.That(msg.UShort, Is.EqualTo(0x1234));
            Assert.That(msg.Int, Is.EqualTo(-3));
            Assert.That(msg.UInt, Is.EqualTo(0x12345678));
            Assert.That(msg.Long, Is.EqualTo(-4));
            Assert.That(msg.ULong, Is.EqualTo(0x0102030405060708));
            Assert.That(msg.Enum, Is.EqualTo(BigEndianEnum.Value));
            Assert.That(msg.Array, Is.EqualTo(new ushort[] {0x0001, 0xABCD}));
            Assert.That(msg.EnumArray, Is.EqualTo(new[] {BigEndianEnum.Value, BigEndianEnum.Value}));
            Assert.That(msg.LittleEndian, Is.EqualTo(0x1234));
        }

        [Test]
        public void Pack()
        {
            var msg = new BigEndianTest();
            msg.Unpack(Data);

            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(Data.Length));
            Assert.That(buffer, Is.EqualTo(Data));
        }

        [TestCase("float")]
        [TestCase("byte")]
        [TestCase("byte[]")]
        public void UnsupportedTypesAreRejected(string type)
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace BigEndianTest
{
    [Aero]
    public partial class Test
    {
        [AeroBigEndian]" + (type.EndsWith("[]") ? "[AeroArray(2)]" : "") + @"
        public " + type + @" Value;
    }
}");

            Assert.That(result.GeneratorDiagnosticIds, Does.Contain("Aero18"));
        }

        [Test]
        public void UnsupportedTypesInBlocksAreRejected()
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace BigEndianTest
{
    [AeroBlock]
    public struct Block
    {
        [AeroBigEndian]
        public byte Value;
    }

    [Aero]
    public partial class Test
    {
        public Block Value;
    }
}");

            Assert.That(result.GeneratorDiagnosticIds, Does.Contain("Aero18"));
            Assert.That(result.GeneratorDiagnosticIds, Does.Not.Contain("Aero2"));
        }
    }
}
