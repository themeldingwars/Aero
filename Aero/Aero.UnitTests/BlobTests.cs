using System;
using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class BlobReadToEndTest
    {
        [AeroBlob]
        public byte[] Ticket;
    }

    [Aero]
    public partial class BlobLoginTest
    {
        public ushort Id;
        public uint   Flags;
        [AeroBlob]
        public byte[] Ticket;
    }

    [Aero]
    public partial class BlobLenPrefixedTest
    {
        public byte Header;
        [AeroBlob(typeof(ushort))]
        public byte[] Dat2;
    }

    [Aero]
    public partial class BlobRefFieldTest
    {
        public uint Length;
        [AeroBlob(nameof(Length))]
        public byte[] Dat3;
    }

    public class BlobTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void BlobReadToEndEmptyTest()
        {
            var testObject = new BlobReadToEndTest();
            var readBytes  = testObject.Unpack(ReadOnlySpan<byte>.Empty);
            if (readBytes == 0 && testObject.Ticket != null && testObject.Ticket.Length == 0) {
                Assert.Pass("Empty read-to-end blob read zero bytes");
            }

            Assert.Fail($"Expected 0 bytes read and an empty blob, got {readBytes} read and {(testObject.Ticket?.Length ?? -1)} blob length");
        }

        private static byte[] MakeBlobBytes(int length, byte start = 1)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++) {
                bytes[i] = (byte) (start + i);
            }

            return bytes;
        }

        [Test]
        public void BlobReadToEndTest()
        {
            var source     = MakeBlobBytes(250);
            var testObject = new BlobReadToEndTest();
            var readBytes  = testObject.Unpack(source.AsSpan());
            if (readBytes == source.Length && testObject.Ticket != null && testObject.Ticket.SequenceEqual(source)) {
                Assert.Pass("Read-to-end blob read all the remaining bytes");
            }

            Assert.Fail($"Expected {source.Length} bytes read and matching blob, got {readBytes} read");
        }

        [Test]
        public void BlobLoginNoBlobTest()
        {
            var data = new byte[] { 0x2A, 0x01, 0x0F, 0x00, 0x00, 0x00 };
            var testObject = new BlobLoginTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == 6 && testObject.Id == 0x012A && testObject.Flags == 0x0F &&
                testObject.Ticket != null && testObject.Ticket.Length == 0) {
                Assert.Pass("Login message with no trailing blob read an empty blob");
            }

            Assert.Fail($"Expected 6 bytes read with an empty blob, got {readBytes} read, Id: {testObject.Id}, Flags: {testObject.Flags}, blob: {(testObject.Ticket?.Length ?? -1)}");
        }

        [Test]
        public void BlobLoginWithBlobTest()
        {
            var ticket   = MakeBlobBytes(220, 100);
            var data     = new byte[6 + ticket.Length];
            data[0]      = 0x2A;
            data[1]      = 0x01;
            data[2]      = 0x0F;
            ticket.CopyTo(data, 6);

            var testObject = new BlobLoginTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == data.Length && testObject.Id == 0x012A && testObject.Flags == 0x0F &&
                testObject.Ticket != null && testObject.Ticket.SequenceEqual(ticket)) {
                Assert.Pass("Login message with trailing blob read the whole blob");
            }

            Assert.Fail($"Expected {data.Length} bytes read and matching blob, got {readBytes} read");
        }

        [Test]
        public void BlobLenPrefixedTest()
        {
            var payload = MakeBlobBytes(10, 50);
            var data    = new byte[3 + payload.Length];
            data[0]     = 0xAB;
            data[1]     = 0x0A; // ushort length 10 little endian
            data[2]     = 0x00;
            payload.CopyTo(data, 3);

            var testObject = new BlobLenPrefixedTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == data.Length && testObject.Header == 0xAB &&
                testObject.Dat2 != null && testObject.Dat2.SequenceEqual(payload)) {
                Assert.Pass("Length prefixed blob read the prefix and payload");
            }

            Assert.Fail($"Expected {data.Length} bytes read and matching blob, got {readBytes} read, blob: {(testObject.Dat2?.Length ?? -1)}");
        }

        [Test]
        public void BlobLenPrefixedZeroTest()
        {
            var data = new byte[] { 0xAB, 0x00, 0x00 };
            var testObject = new BlobLenPrefixedTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == 3 && testObject.Header == 0xAB && testObject.Dat2 != null && testObject.Dat2.Length == 0) {
                Assert.Pass("Length prefixed blob with a zero prefix read an empty blob");
            }

            Assert.Fail($"Expected 3 bytes read and an empty blob, got {readBytes} read, blob: {(testObject.Dat2?.Length ?? -1)}");
        }

        [Test]
        public void BlobRefFieldTest()
        {
            var payload = MakeBlobBytes(5, 7);
            var data    = new byte[4 + payload.Length];
            data[0]     = 0x05;
            data[1]     = 0x00;
            data[2]     = 0x00;
            data[3]     = 0x00;
            payload.CopyTo(data, 4);

            var testObject = new BlobRefFieldTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == data.Length && testObject.Length == 5 &&
                testObject.Dat3 != null && testObject.Dat3.SequenceEqual(payload)) {
                Assert.Pass("Ref length blob read the referenced length and payload");
            }

            Assert.Fail($"Expected {data.Length} bytes read and matching blob, got {readBytes} read, blob: {(testObject.Dat3?.Length ?? -1)}");
        }

        [Test]
        public void BlobRefFieldZeroTest()
        {
            var data = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            var testObject = new BlobRefFieldTest();
            var readBytes  = testObject.Unpack(data.AsSpan());
            if (readBytes == 4 && testObject.Length == 0 && testObject.Dat3 != null && testObject.Dat3.Length == 0) {
                Assert.Pass("Ref length blob with a zero length read an empty blob");
            }

            Assert.Fail($"Expected 4 bytes read and an empty blob, got {readBytes} read, blob: {(testObject.Dat3?.Length ?? -1)}");
        }

        [Test]
        public void BlobPackUnpackRoundTripTest()
        {
            var ticket   = MakeBlobBytes(128, 200);
            var login    = new BlobLoginTest {Id = 0x012A, Flags = 0x0F, Ticket = ticket};
            var prefixed = new BlobLenPrefixedTest {Header = 0xAB, Dat2 = MakeBlobBytes(33, 42)};
            var refField = new BlobRefFieldTest {Length = 7, Dat3 = MakeBlobBytes(7, 9)};

            var buffer = new byte[login.GetPackedSize() + prefixed.GetPackedSize() + refField.GetPackedSize()];
            var offset = 0;
            offset += login.Pack(buffer.AsSpan(offset));
            offset += prefixed.Pack(buffer.AsSpan(offset));
            offset += refField.Pack(buffer.AsSpan(offset));

            if (offset != login.GetPackedSize() + prefixed.GetPackedSize() + refField.GetPackedSize()) {
                Assert.Fail("Packed size didn't match the sum of the GetPackedSize values");
            }

            var repackedLogin = new BlobLoginTest();
            if (repackedLogin.Unpack(buffer.AsSpan(0, login.GetPackedSize())) != login.GetPackedSize() ||
                repackedLogin.Id != login.Id || repackedLogin.Flags != login.Flags ||
                repackedLogin.Ticket == null || !repackedLogin.Ticket.SequenceEqual(login.Ticket)) {
                Assert.Fail("Login blob didn't round trip");
            }

            var repackedPrefixed = new BlobLenPrefixedTest();
            var prefixedSize     = prefixed.GetPackedSize();
            if (repackedPrefixed.Unpack(buffer.AsSpan(login.GetPackedSize(), prefixedSize)) != prefixedSize ||
                repackedPrefixed.Header != prefixed.Header ||
                repackedPrefixed.Dat2 == null || !repackedPrefixed.Dat2.SequenceEqual(prefixed.Dat2)) {
                Assert.Fail("Length prefixed blob didn't round trip");
            }

            var repackedRef = new BlobRefFieldTest();
            var refSize     = refField.GetPackedSize();
            if (repackedRef.Unpack(buffer.AsSpan(login.GetPackedSize() + prefixedSize, refSize)) != refSize ||
                repackedRef.Length != refField.Length ||
                repackedRef.Dat3 == null || !repackedRef.Dat3.SequenceEqual(refField.Dat3)) {
                Assert.Fail("Ref length blob didn't round trip");
            }

            Assert.Pass("All blob round trips matched");
        }

        [Test]
        public void BlobZeroLengthRoundTripTest()
        {
            var login    = new BlobLoginTest {Id = 1, Flags = 0, Ticket = Array.Empty<byte>()};
            var prefixed = new BlobLenPrefixedTest {Header = 1, Dat2 = Array.Empty<byte>()};

            var buffer = new byte[login.GetPackedSize() + prefixed.GetPackedSize()];
            var offset = 0;
            offset += login.Pack(buffer.AsSpan(offset));
            offset += prefixed.Pack(buffer.AsSpan(offset));

            if (login.GetPackedSize() != 6 || prefixed.GetPackedSize() != 3 || offset != 9) {
                Assert.Fail($"Expected 6 and 3 byte packed sizes for empty blobs, got {login.GetPackedSize()} and {prefixed.GetPackedSize()}");
            }

            var repacked = new BlobLoginTest();
            if (repacked.Unpack(buffer.AsSpan(0, 6)) != 6 || repacked.Ticket == null || repacked.Ticket.Length != 0) {
                Assert.Fail("Empty login blob didn't round trip");
            }

            Assert.Pass("Zero length blobs packed and unpacked");
        }
    }
}
