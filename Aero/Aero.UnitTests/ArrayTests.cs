using System;
using System.Numerics;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [AeroBlock]
    public struct ArrayBlockItem
    {
        public uint  A;
        public float B;
    }

    [Aero]
    public partial class IntArrayFixedTest
    {
        [AeroArray(4)]
        public int[] ArrayTest;
    }

    [Aero]
    public partial class IntArrayRefLenTest
    {
        public int IntArrayRefLen;
        [AeroArray(nameof(IntArrayRefLen))]
        public int[] ArrayTest;
    }
    
    [Aero]
    public partial class IntArrayReadToEndTest
    {
        [AeroArray(-4)]
        public int[] ArrayTest;
    }
    
    [Aero]
    public partial class IntArrayTypeLenTest
    {
        [AeroArray(typeof(int))]
        public int[] ArrayTest;
    }
    
    [Aero]
    public partial class ByteArrayFixedTest
    {
        [AeroArray(10)]
        public byte[] ArrayTest;
    }
    
    [Aero]
    public partial class ArrayBlockItemArrayFixedTest
    {
        [AeroArray(2)]
        public ArrayBlockItem[] ArrayTest;
    }

    [Aero]
    public partial class ArrayBlockItemArrayTypeLenTest
    {
        [AeroArray(typeof(int))]
        public ArrayBlockItem[] ArrayTest;
    }
    
    [Aero]
    public partial class ArrayOfVector2sFixedTest
    {
        [AeroArray(2)]
        public Vector2[] Vector2Test;
    }

    [Aero]
    public partial class AeroBlockWithSeparateArrayCountContainer
    {
        public int Test;

        public AeroBlockWithSeparateArrayCount ArrayContainer;
    }

    [AeroBlock]
    public struct AeroBlockWithSeparateArrayCount
    {
        public int Count;

        [AeroArray(nameof(Count))]
        public uint[] Items;
    }

    public class ArrayTests
    {
        [SetUp]
        public void Setup()
        {
        }

        private static byte[] IntArrayFixedTestBytes = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00};

        [Test]
        public void IntArrayFixedTester()
        {
            var testObject = new IntArrayFixedTest();
            if (testObject.Unpack(IntArrayFixedTestBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 4) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.ArrayTest[0] == 1 &&
                    testObject.ArrayTest[1] == 2 &&
                    testObject.ArrayTest[2] == 3 &&
                    testObject.ArrayTest[3] == 4) {
                    Assert.Pass("Array read and values matched");
                }
                
                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void IntArrayReadToEndTester()
        {
            var testObject = new IntArrayReadToEndTest();
            if (testObject.Unpack(IntArrayFixedTestBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 4) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.ArrayTest[0] == 1 &&
                    testObject.ArrayTest[1] == 2 &&
                    testObject.ArrayTest[2] == 3 &&
                    testObject.ArrayTest[3] == 4) {
                    Assert.Pass("Array read and values matched");
                }
                
                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        private static byte[] IntArrayVarestBytes = new byte[] { 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00};
        [Test]
        public void IntArrayRefTester()
        {
            var testObject = new IntArrayRefLenTest();
            if (testObject.Unpack(IntArrayVarestBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 4) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.ArrayTest[0] == 1 &&
                    testObject.ArrayTest[1] == 2 &&
                    testObject.ArrayTest[2] == 3 &&
                    testObject.ArrayTest[3] == 4) {
                    Assert.Pass("Array read and values matched");
                }
                
                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void IntArrayTypeTester()
        {
            var testObject = new IntArrayTypeLenTest();
            if (testObject.Unpack(IntArrayVarestBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 4) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.ArrayTest[0] == 1 &&
                    testObject.ArrayTest[1] == 2 &&
                    testObject.ArrayTest[2] == 3 &&
                    testObject.ArrayTest[3] == 4) {
                    Assert.Pass("Array read and values matched");
                }
                
                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        private static byte[] ByteArrayFixedTestBytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 ,9 };

        
        [Test]
        public void ByteArrayFixedTest()
        {
            var testObject = new ByteArrayFixedTest();
            if (testObject.Unpack(ByteArrayFixedTestBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 10) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                for (int i = 0; i < 9; i++) {
                    if (testObject.ArrayTest[i] != i) {
                        Assert.Fail("Array values didn't match");
                    }
                }
                
                Assert.Pass("Array read and values matched");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }

        private static byte[] ArrayBlockItemArrayFixedBytes = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x41,
                                                                           0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA4, 0x41 };

        [Test]
        public void ArrayBlockItemArrayFixedTester()
        {
            var testObject = new ArrayBlockItemArrayFixedTest();
            if (testObject.Unpack(ArrayBlockItemArrayFixedBytes.AsSpan()) > -1) {
                if (testObject.ArrayTest.Length != 2) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.ArrayTest[0].A == 1 &&
                    testObject.ArrayTest[0].B == 10.5f &&
                    testObject.ArrayTest[1].A == 2 &&
                    testObject.ArrayTest[1].B == 20.5f) {
                    Assert.Pass("Array read and values matched");
                }
                
                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void ArrayOfVector2SFixedTester()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
            var testObject = new ArrayOfVector2sFixedTest();
            if (testObject.Unpack(data) > -1) {
                if (testObject.Vector2Test.Length != 2) {
                    Assert.Fail("Didn't read all the items in the array");
                }

                if (testObject.Vector2Test[0].X == 1f && testObject.Vector2Test[0].Y == 2f && testObject.Vector2Test[1].X == 3f && testObject.Vector2Test[1].Y == 4f) {
                    Assert.Pass();
                }

                Assert.Fail("Array values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void AeroBlockWithSeprateArrayCountContainerTester()
        {
            ReadOnlySpan<byte> data       = new byte[] { 0x0A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 };
            var                testObject = new AeroBlockWithSeparateArrayCountContainer();
            if (testObject.Unpack(data) > -1) {
                if (testObject.Test != 10) {
                    Assert.Fail("Test value didn't match");
                }

                if (testObject.ArrayContainer.Count == 2 && testObject.ArrayContainer.Items[0] == 1 && testObject.ArrayContainer.Items[1] == 2) {
                    Assert.Pass();
                }

                Assert.Fail("Values didn't match");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
    }
}