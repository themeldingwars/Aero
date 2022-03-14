using System.Linq;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero(true)]
    public partial class SimpleView
    {
        public int   TestValue;
        public float TestVlaue2;

        [AeroArray(2)] public int[] TestArrayValues;
    }

    [Aero(true)]
    public partial class SimpleViewWithNullable
    {
        public int   TestValue;
        public float TestVlaue2;

        [AeroArray(2)] public int[] TestArrayValues;

        [AeroNullable] public int TestValueNullable;
    }

    public class ViewTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void SimpleViewPackTest()
        {
            var test = new SimpleView
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1},
            };

            var bytes = new byte[]
            {
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.Pack(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewUnpackTest()
        {
            var test = new SimpleView
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1}
            };

            var unpacked = new SimpleView();

            var bytes = new byte[]
            {
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            };
            var lenUnpacked = unpacked.Unpack(bytes);

            if (lenUnpacked                 == bytes.Length &&
                unpacked.TestValue          == 1
             && unpacked.TestVlaue2         == 2.0f
             && unpacked.TestArrayValues[0] == 0
             && unpacked.TestArrayValues[1] == 1) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewPackChangesTest()
        {
            var test = new SimpleView();
            test.TestValueProp  = 1;
            test.TestVlaue2Prop = 2.0f;

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                0x01,
                0x00, 0x00, 0x00, 0x40
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewUnpackChangesTest()
        {
            var test = new SimpleView();

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                0x01,
                0x00, 0x00, 0x00, 0x40
            };
            var lenPacked = test.UnpackChanges(bytes);

            if (lenPacked       == bytes.Length &&
                test.TestValue  == 1            &&
                test.TestVlaue2 == 2.0f) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewWithNullableNullPackTest()
        {
            var test = new SimpleViewWithNullable
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1}
            };

            test.TestValueNullableProp = null;

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.Pack(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewWithNullableNotNullPackTest()
        {
            var test = new SimpleViewWithNullable
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1}
            };

            test.TestValueNullableProp = 2;

            var bytes = new byte[]
            {
                0x01,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.Pack(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewWithNullableNullUnpackTest()
        {
            var test = new SimpleViewWithNullable
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1}
            };

            test.TestValueNullableProp = null;

            var unpacked = new SimpleViewWithNullable();

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00
            };
            var lenUnpacked = unpacked.Unpack(bytes);

            if (lenUnpacked                    == bytes.Length &&
                unpacked.TestValue             == 1
             && unpacked.TestVlaue2            == 2.0f
             && unpacked.TestArrayValues[0]    == 0
             && unpacked.TestArrayValues[1]    == 1
             && unpacked.TestValueNullableProp == null) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewWithNullableNotNullUnpackTest()
        {
            var test = new SimpleViewWithNullable
            {
                TestValue       = 1,
                TestVlaue2      = 2.0f,
                TestArrayValues = new[] {0, 1}
            };

            test.TestValueNullableProp = 2;

            var unpacked = new SimpleViewWithNullable();

            var bytes = new byte[]
            {
                0x01,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00
            };
            var lenUnpacked = unpacked.Unpack(bytes);

            if (lenUnpacked                 == bytes.Length &&
                unpacked.TestValue          == 1
             && unpacked.TestVlaue2         == 2.0f
             && unpacked.TestArrayValues[0] == 0
             && unpacked.TestArrayValues[1] == 1
             && unpacked.TestValueNullable  == 2) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewPackChangesWithNullableNotNullTest()
        {
            var test = new SimpleViewWithNullable();
            test.TestValueProp         = 1;
            test.TestValueNullableProp = 2;

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                0x03,
                0x02, 0x00, 0x00, 0x00
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewPackChangesWithNullableNullTest()
        {
            var test = new SimpleViewWithNullable();
            test.TestValueProp         = 1;
            test.TestValueNullableProp = null;

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x00, 0x00, 0x00,
                131
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked    = test.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void SimpleViewGetPackedChangesSizeTest()
        {
            var test = new SimpleView();
            test.TestValueProp  = 1;
            test.TestVlaue2Prop = 2.0f;

            var lenPacked = test.GetPackedChangesSize();

            if (lenPacked == 10) {
                Assert.Pass();
            }

            Assert.Fail();
        }
    }
}