using System;
using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class ChunkedArrayTest
    {
        [AeroArray(typeof(byte), Chunked = true)]
        public uint[] Items;

        public byte Trailer;
    }

    [AeroBlock]
    public struct ChunkedArrayGroup
    {
        public byte Id;

        [AeroArray(typeof(byte), Chunked = true)]
        public ushort[] Entries;
    }

    [Aero]
    public partial class ChunkedArrayNestedTest
    {
        [AeroArray(typeof(byte), Chunked = true)]
        public ChunkedArrayGroup[] Groups;

        public byte Trailer;
    }

    public class ChunkedArrayTests
    {
        private static uint[] MakeItems(int count) => Enumerable.Range(0, count).Select(x => (uint)x * 3 + 1).ToArray();

        private static byte[] Pack(ChunkedArrayTest msg)
        {
            var buffer  = new byte[msg.GetPackedSize()];
            var written = msg.Pack(buffer);
            Assert.That(written, Is.EqualTo(buffer.Length), "Pack length doesn't match GetPackedSize");
            return buffer;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(254)]
        [TestCase(255)]
        [TestCase(256)]
        [TestCase(509)]
        [TestCase(510)]
        [TestCase(765)]
        public void RoundTrip(int count)
        {
            var msg    = new ChunkedArrayTest {Items = MakeItems(count), Trailer = 0xAB};
            var packed = Pack(msg);

            Assert.That(packed.Length, Is.EqualTo(count / 255 + 1 + count * 4 + 1));

            var read = new ChunkedArrayTest();
            Assert.That(read.Unpack(packed), Is.EqualTo(packed.Length));
            Assert.That(read.Items, Is.EqualTo(msg.Items));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }

        [Test]
        public void EmptyIsSingleZeroCount()
        {
            var packed = Pack(new ChunkedArrayTest {Items = Array.Empty<uint>(), Trailer = 0xAB});
            Assert.That(packed, Is.EqualTo(new byte[] {0x00, 0xAB}));
        }

        [Test]
        public void ExactlyOneFullChunkEndsWithZeroCount()
        {
            var packed = Pack(new ChunkedArrayTest {Items = MakeItems(255), Trailer = 0xAB});

            Assert.That(packed[0], Is.EqualTo(0xFF));
            Assert.That(packed[1 + 255 * 4], Is.EqualTo(0x00));
            Assert.That(packed[2 + 255 * 4], Is.EqualTo(0xAB));
            Assert.That(packed.Length, Is.EqualTo(3 + 255 * 4));
        }

        [Test]
        public void SecondChunkHasItsOwnCount()
        {
            var packed = Pack(new ChunkedArrayTest {Items = MakeItems(256), Trailer = 0xAB});

            Assert.That(packed[0], Is.EqualTo(0xFF));
            Assert.That(packed[1 + 255 * 4], Is.EqualTo(0x01));
            Assert.That(BitConverter.ToUInt32(packed, 2 + 255 * 4), Is.EqualTo(255u * 3 + 1));
            Assert.That(packed[6 + 255 * 4], Is.EqualTo(0xAB));
        }

        [Test]
        public void ReadsClientChunks()
        {
            // 256 elements as the client sends them: a full chunk of 255, then a chunk of 1
            var data = new byte[1 + 255 * 4 + 1 + 4 + 1];
            data[0] = 0xFF;
            for (int i = 0; i < 255; i++) {
                BitConverter.GetBytes((uint)i).CopyTo(data, 1 + i * 4);
            }

            data[1 + 255 * 4] = 0x01;
            BitConverter.GetBytes(0xDEADBEEFu).CopyTo(data, 2 + 255 * 4);
            data[^1] = 0x42;

            var read = new ChunkedArrayTest();
            Assert.That(read.Unpack(data), Is.EqualTo(data.Length));
            Assert.That(read.Items.Length, Is.EqualTo(256));
            Assert.That(read.Items[254], Is.EqualTo(254u));
            Assert.That(read.Items[255], Is.EqualTo(0xDEADBEEFu));
            Assert.That(read.Trailer, Is.EqualTo(0x42));
        }

        [Test]
        public void NestedChunkedArrays()
        {
            var msg = new ChunkedArrayNestedTest
            {
                Groups = Enumerable.Range(0, 257).Select(g => new ChunkedArrayGroup
                {
                    Id      = (byte)g,
                    Entries = Enumerable.Range(0, g % 3 == 0 ? 255 : g % 7).Select(e => (ushort)(g * 1000 + e)).ToArray()
                }).ToArray(),
                Trailer = 0x99
            };

            var buffer  = new byte[msg.GetPackedSize()];
            var written = msg.Pack(buffer);
            Assert.That(written, Is.EqualTo(buffer.Length));

            var read = new ChunkedArrayNestedTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.Groups.Length, Is.EqualTo(257));
            for (int i = 0; i < msg.Groups.Length; i++) {
                Assert.That(read.Groups[i].Id, Is.EqualTo(msg.Groups[i].Id));
                Assert.That(read.Groups[i].Entries, Is.EqualTo(msg.Groups[i].Entries));
            }

            Assert.That(read.Trailer, Is.EqualTo(0x99));
        }
    }
}
