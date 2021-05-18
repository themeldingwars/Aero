using NUnit.Framework;

namespace Aero.UnitTests
{
    public class GetWriteLengthTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void SimpleTypesGetPackedLength()
        {
            var packedSize = Tests.SimpleTypesRef.GetPackedSize();
            if (packedSize == 42) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void IfChecksIfPassGetPackedLength()
        {
            var test = new IfTest1
            {
                IfValue = 1,
                ToReadIfValueMatches = 2
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == 8) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void IfChecksIfFailGetPackedLength()
        {
            var test = new IfTest1
            {
                IfValue = 2
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == 4) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayFixedTypeGetPackedLength()
        {
            var test = new IntArrayFixedTest
            {
                ArrayTest = new []{ 0, 1, 2, 3 }
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == 4 * 4) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayLenTypeTypeGetPackedLength()
        {
            var test = new IntArrayTypeLenTest
            {
                ArrayTest = new []{ 0, 1 }
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == (4 * 2) + 4) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayFixedLenBlockTypeGetPackedLength()
        {
            var test = new ArrayBlockItemArrayFixedTest
            {
                ArrayTest = new []{ new ArrayBlockItem(), new ArrayBlockItem()}
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == (8 * 2)) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayTypeLenBlockTypeGetPackedLength()
        {
            var test = new ArrayBlockItemArrayTypeLenTest
            {
                ArrayTest = new []{ new ArrayBlockItem(), new ArrayBlockItem()}
            };
            var packedSize = test.GetPackedSize();
            if (packedSize == (8 * 2)) {
                Assert.Pass();
            }
            else {
                Assert.Fail();
            }
        }
    }
}