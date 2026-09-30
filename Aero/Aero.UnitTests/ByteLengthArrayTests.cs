using System;
using System.IO;
using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;
using static Aero.Gen.Attributes.AeroIfAttribute;

namespace Aero.UnitTests
{
    [AeroBlock]
    public struct ByteLengthFieldChange
    {
        public byte Index;
        public uint Value;
    }

    // The shape of the SinCard changes in EncounterUIUpdate: a full card when the guid's low byte is 0xFF, field changes otherwise
    [AeroBlock]
    public struct ByteLengthCardChange
    {
        public ulong Guid;

        // Not serialized, only used by the conditions below
        public byte GuidLowByte => (byte)Guid;

        [AeroIf(nameof(GuidLowByte), 0xFF)]
        public ulong Target;

        [AeroIf(nameof(GuidLowByte), 0xFF)]
        public uint Type;

        [AeroIf(nameof(GuidLowByte), Ops.NotEqual, 0xFF)]
        public byte NumChanges;

        [AeroIf(nameof(GuidLowByte), Ops.NotEqual, 0xFF)]
        [AeroArray(nameof(NumChanges))]
        public ByteLengthFieldChange[] Changes;
    }

    [Aero]
    public partial class ByteLengthArrayTest
    {
        [AeroArray(typeof(ushort), LengthIsBytes = true)]
        public ByteLengthCardChange[] Cards;

        public byte Trailer;
    }

    // Reads nothing, the condition is never true
    [AeroBlock]
    public struct ByteLengthEmptyElement
    {
        public byte Never => 0;

        [AeroIf(nameof(Never), 1)]
        public byte Value;
    }

    [Aero]
    public partial class ByteLengthEmptyElementTest
    {
        [AeroArray(typeof(byte), LengthIsBytes = true)]
        public ByteLengthEmptyElement[] Elements;
    }

    [Aero]
    public partial class ByteLengthFixedSizeTest
    {
        [AeroArray(typeof(byte), LengthIsBytes = true)]
        public uint[] Values;

        public byte Trailer;
    }

    public class ByteLengthArrayTests
    {
        private static readonly ByteLengthCardChange FullCard = new()
        {
            Guid = 0x11223344556677FF, Target = 0x0102030405060708, Type = 7
        };

        private static readonly ByteLengthCardChange RemovedCard = new()
        {
            Guid = 0x1122334455667701, NumChanges = 0, Changes = Array.Empty<ByteLengthFieldChange>()
        };

        private static readonly ByteLengthCardChange ChangedCard = new()
        {
            Guid       = 0x1122334455667702,
            NumChanges = 2,
            Changes    = new[] {new ByteLengthFieldChange {Index = 1, Value = 10}, new ByteLengthFieldChange {Index = 3, Value = 30}}
        };

        private static byte[] Pack(ByteLengthArrayTest msg)
        {
            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(buffer.Length));
            return buffer;
        }

        [Test]
        public void Empty()
        {
            var packed = Pack(new ByteLengthArrayTest {Cards = Array.Empty<ByteLengthCardChange>(), Trailer = 0xAB});
            Assert.That(packed, Is.EqualTo(new byte[] {0x00, 0x00, 0xAB}));

            var read = new ByteLengthArrayTest();
            Assert.That(read.Unpack(packed), Is.EqualTo(packed.Length));
            Assert.That(read.Cards, Is.Empty);
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }

        [Test]
        public void SingleElement()
        {
            var packed = Pack(new ByteLengthArrayTest {Cards = new[] {FullCard}, Trailer = 0xAB});
            Assert.That(BitConverter.ToUInt16(packed, 0), Is.EqualTo(8 + 8 + 4));
            Assert.That(packed.Length, Is.EqualTo(2 + 20 + 1));

            var read = new ByteLengthArrayTest();
            Assert.That(read.Unpack(packed), Is.EqualTo(packed.Length));
            Assert.That(read.Cards.Length, Is.EqualTo(1));
            Assert.That(read.Cards[0].Target, Is.EqualTo(FullCard.Target));
            Assert.That(read.Cards[0].Type, Is.EqualTo(7));
        }

        [Test]
        public void ElementsOfDifferentSizes()
        {
            var cards  = new[] {FullCard, RemovedCard, ChangedCard, FullCard, ChangedCard};
            var packed = Pack(new ByteLengthArrayTest {Cards = cards, Trailer = 0xAB});
            Assert.That(BitConverter.ToUInt16(packed, 0), Is.EqualTo(packed.Length - 3));

            var read = new ByteLengthArrayTest();
            Assert.That(read.Unpack(packed), Is.EqualTo(packed.Length));
            Assert.That(read.Cards.Select(x => x.Guid), Is.EqualTo(cards.Select(x => x.Guid)));
            Assert.That(read.Cards[1].NumChanges, Is.EqualTo(0));
            Assert.That(read.Cards[2].Changes.Select(x => x.Value), Is.EqualTo(new uint[] {10, 30}));
            Assert.That(read.Cards[3].Target, Is.EqualTo(FullCard.Target));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));

            Assert.That(Pack(read), Is.EqualTo(packed));
        }

        [Test]
        public void OverrunIsAnError()
        {
            var packed = Pack(new ByteLengthArrayTest {Cards = new[] {FullCard}, Trailer = 0xAB});
            packed[0] = 10; // the card is 20 bytes

            Action unpack = () => new ByteLengthArrayTest().Unpack(packed);
            Assert.Throws<InvalidDataException>(unpack);
        }

        [Test]
        public void LengthPastTheEndIsAnError()
        {
            var packed = Pack(new ByteLengthArrayTest {Cards = new[] {FullCard}, Trailer = 0xAB});
            packed[0] = 0xFF;
            packed[1] = 0xFF;

            Action unpack = () => new ByteLengthArrayTest().Unpack(packed);
            Assert.Throws<InvalidDataException>(unpack);
        }

        [Test]
        public void ElementThatReadsNothingIsAnError()
        {
            Action unpack = () => new ByteLengthEmptyElementTest().Unpack(new byte[] {0x02, 0x00, 0x00});
            Assert.Throws<InvalidDataException>(unpack);
        }

        [Test]
        public void ReadLogsTileThePayload()
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace ByteLengthTest
{
    [AeroBlock]
    public struct Entry
    {
        public byte Id;

        [AeroString(typeof(byte))]
        public string Name;
    }

    [Aero]
    public partial class Test
    {
        [AeroArray(typeof(ushort), LengthIsBytes = true)]
        public Entry[] Entries;

        public byte Trailer;
    }
}", diagLogging: true);
            Assert.That(result.CompileErrors, Is.Empty);

            var data = new byte[] {0x07, 0x00, 0x01, 0x02, 0x68, 0x69, 0x02, 0x01, 0x78, 0xAB};
            var msg  = (Aero.Gen.IAero)result.Create("ByteLengthTest.Test");
            Assert.That(msg.Unpack(data), Is.EqualTo(data.Length));

            var offset = 0;
            foreach (var entry in msg.GetDiagReadLogs()
                                     .Where(x => x.EntryType is Gen.AeroReadLog.LogEntryType.Field or Gen.AeroReadLog.LogEntryType.Length)
                                     .OrderBy(x => x.Offset)) {
                Assert.That(entry.Offset, Is.EqualTo(offset), $"{entry}");
                offset += entry.Length;
            }

            Assert.That(offset, Is.EqualTo(data.Length));
        }

        [Test]
        public void FixedSizeElements()
        {
            var msg    = new ByteLengthFixedSizeTest {Values = new uint[] {1, 2, 3}, Trailer = 0xAB};
            var buffer = new byte[msg.GetPackedSize()];
            Assert.That(msg.Pack(buffer), Is.EqualTo(1 + 12 + 1));
            Assert.That(buffer[0], Is.EqualTo(12));

            var read = new ByteLengthFixedSizeTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.Values, Is.EqualTo(msg.Values));
            Assert.That(read.Trailer, Is.EqualTo(0xAB));
        }

        [Test]
        public void LengthTooLargeForItsTypeIsAnError()
        {
            var msg    = new ByteLengthFixedSizeTest {Values = new uint[64], Trailer = 0xAB};
            var buffer = new byte[msg.GetPackedSize()];

            Assert.Throws<InvalidOperationException>(() => msg.Pack(buffer));
        }

        [Test]
        public void NegativeLengthIsAnError()
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace ByteLengthTest
{
    [Aero]
    public partial class Test
    {
        [AeroArray(typeof(int), LengthIsBytes = true)]
        public uint[] Values;
    }
}");
            Assert.That(result.CompileErrors, Is.Empty);

            var msg = (Aero.Gen.IAero)result.Create("ByteLengthTest.Test");
            Assert.Throws<InvalidDataException>(() => msg.Unpack(new byte[] {0xFC, 0xFF, 0xFF, 0xFF}));
        }

        [TestCase("ulong")]
        [TestCase("float")]
        public void UnsupportedLengthTypesAreRejected(string type)
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace ByteLengthTest
{
    [Aero]
    public partial class Test
    {
        [AeroArray(typeof(" + type + @"), LengthIsBytes = true)]
        public uint[] Values;
    }
}");

            Assert.That(result.GeneratorDiagnosticIds, Does.Contain("Aero16"));
        }
    }
}
