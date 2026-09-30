using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class StringRoundTripTest
    {
        [AeroString]
        public string NullTerminated;

        [AeroString(typeof(byte))]
        public string Prefixed;

        [AeroString(6)]
        public string Fixed;

        public byte Trailer;
    }

    public class StringEncodingTests
    {
        private static byte[] Pack(Gen.IAero msg)
        {
            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(buffer.Length), "Pack length doesn't match GetPackedSize");
            return buffer;
        }

        [Test]
        public void NonAsciiStringsUseTheirByteLength()
        {
            var msg = new StringRoundTripTest
            {
                NullTerminated = "qqeыыы",
                Prefixed       = "Grüße",
                Fixed          = "ыы",
                Trailer        = 0xAB
            };

            var packed = Pack(msg);
            var nullTerminatedBytes = Encoding.UTF8.GetBytes("qqeыыы\0");
            var prefixedBytes       = Encoding.UTF8.GetBytes("Grüße");
            Assert.That(packed.Take(nullTerminatedBytes.Length), Is.EqualTo(nullTerminatedBytes));
            Assert.That(packed[nullTerminatedBytes.Length], Is.EqualTo(prefixedBytes.Length));
            Assert.That(packed.Length, Is.EqualTo(nullTerminatedBytes.Length + 1 + prefixedBytes.Length + 6 + 1));

            var read = new StringRoundTripTest();
            Assert.That(read.Unpack(packed), Is.EqualTo(packed.Length));
            Assert.That(read.NullTerminated, Is.EqualTo(msg.NullTerminated));
            Assert.That(read.Prefixed, Is.EqualTo(msg.Prefixed));
            Assert.That(read.Fixed.TrimEnd('\0'), Is.EqualTo(msg.Fixed));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }

        [Test]
        public void PrefixedStringTooLongForItsLengthIsAnError()
        {
            var msg    = new StringRoundTripTest {NullTerminated = "", Prefixed = new string('ы', 130), Fixed = "", Trailer = 0xAB};
            var buffer = new byte[msg.GetPackedSize()];

            Assert.Throws<System.InvalidOperationException>(() => msg.Pack(buffer));
        }

        [Test]
        public void FixedStringsArePaddedAndCut()
        {
            var shortString = Pack(new StringRoundTripTest {NullTerminated = "", Prefixed = "", Fixed = "ab", Trailer = 0xAB});
            Assert.That(shortString.Skip(2).Take(6), Is.EqualTo(new byte[] {0x61, 0x62, 0, 0, 0, 0}));
            Assert.That(shortString.Last(), Is.EqualTo(0xAB));

            var longString = Pack(new StringRoundTripTest {NullTerminated = "", Prefixed = "", Fixed = "abcdefgh", Trailer = 0xAB});
            Assert.That(longString.Skip(2).Take(6), Is.EqualTo(Encoding.UTF8.GetBytes("abcdef")));
            Assert.That(longString.Last(), Is.EqualTo(0xAB));

            var splitCharacter = Pack(new StringRoundTripTest {NullTerminated = "", Prefixed = "", Fixed = "abcdeы", Trailer = 0xAB});
            Assert.That(splitCharacter.Skip(2).Take(6), Is.EqualTo(new byte[] {0x61, 0x62, 0x63, 0x64, 0x65, 0}));
        }
    }
}
