using System;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public enum SByteEnum : sbyte
    {
        Negative = -1,
        Zero     = 0,
        Positive = 5
    }

    public enum ShortEnum : short
    {
        Negative = -300,
        Positive = 300
    }

    public enum IntEnum : int
    {
        Negative = -70000,
        Positive = 70000
    }

    public enum LongEnum : long
    {
        Negative = -5000000000,
        Positive = 5000000000
    }

    [Aero]
    public partial class SignedEnumTest
    {
        public SByteEnum SByteValue;
        public ShortEnum ShortValue;
        public IntEnum   IntValue;
        public LongEnum  LongValue;
    }

    public class SignedEnumTests
    {
        [TestCase(SByteEnum.Negative, ShortEnum.Negative, IntEnum.Negative, LongEnum.Negative)]
        [TestCase(SByteEnum.Positive, ShortEnum.Positive, IntEnum.Positive, LongEnum.Positive)]
        public void RoundTrip(SByteEnum sb, ShortEnum s, IntEnum i, LongEnum l)
        {
            var msg = new SignedEnumTest
            {
                SByteValue = sb,
                ShortValue = s,
                IntValue   = i,
                LongValue  = l
            };

            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(1 + 2 + 4 + 8));

            var read = new SignedEnumTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.SByteValue, Is.EqualTo(sb));
            Assert.That(read.ShortValue, Is.EqualTo(s));
            Assert.That(read.IntValue, Is.EqualTo(i));
            Assert.That(read.LongValue, Is.EqualTo(l));
        }

        [Test]
        public void NegativeSByteIsWrittenAsTwosComplement()
        {
            var msg = new SignedEnumTest
            {
                SByteValue = SByteEnum.Negative,
                ShortValue = ShortEnum.Negative
            };

            var buffer = new byte[msg.GetPackedSize()];
            msg.Pack(buffer);

            Assert.That(buffer[0], Is.EqualTo(0xFF));
            Assert.That(BitConverter.ToInt16(buffer, 1), Is.EqualTo(-300));
        }
    }
}
