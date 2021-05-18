using System;
using System.Numerics;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class SimpleTypes
    {
        public byte   Byte;
        public char   Char;
        public int    IntTest;
        public uint   UintTest;
        public short  ShortTest;
        public ushort UshortTest;
        public long   Long;
        public ulong  ULong;
        public float  Float;
        public double Double;
    }

    [AeroBlock]
    public struct SubType
    {
        public byte   Byte;
        public char   Char;
        public int    IntTest;
        public uint   UintTest;
        public short  ShortTest;
        public ushort UshortTest;
        public long   Long;
        public ulong  ULong;
        public float  Float;
        public double Double;
    }
    
    [AeroBlock]
    public struct SubType3
    {
        public int     IntTest;
        public SubType Sub;
    }
    
    [AeroBlock]
    public struct SubTypeSimple
    {
        public int     IntTest;
    }
    
    [Aero]
    public partial class SubTypeTest2
    {
        public int     IntTest;
        public SubType Sub;
    }
    
    [Aero]
    public partial class SubTypeTest3
    {
        public int      IntTest;
        public SubType3 Sub;
    }
    
    [Aero]
    public partial class Vector2Type
    {
        public Vector2 Vec2;
    }
    
    [Aero]
    public partial class Vector3Type
    {
        public Vector3 Vec3;
    }
    
    [Aero]
    public partial class Vector4Type
    {
        public Vector4 Vec4;
    }
    
    [Aero]
    public partial class QuatType
    {
        public Quaternion Quat;
    }
    

    public class Tests
    {
        private static byte[] SimpleTypesBytes = new byte[]
        {
            0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
            0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
            0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40
        };

        public static SimpleTypes SimpleTypesRef = new SimpleTypes()
        {
            Byte       = 1,
            Char       = 'A',
            IntTest    = -100,
            UintTest   = 100,
            ShortTest  = -50,
            UshortTest = 50,
            Long       = -1000000,
            ULong      = 1000000,
            Float      = 1.2f,
            Double     = 2.5d,
        };

        private static byte[] SubType1TypesBytes = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
            0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
            0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40
        };
        
        private static byte[] SubType2TypesBytes = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
            0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
            0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40
        };

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void SimpleTypesTest()
        {
            var test = new SimpleTypes();
            if (test.Unpack(SimpleTypesBytes.AsSpan()) > -1) {
                if (test.Byte                                     == SimpleTypesRef.Byte       &&
                    test.Char                                     == SimpleTypesRef.Char       &&
                    test.IntTest                                  == SimpleTypesRef.IntTest    &&
                    test.UintTest                                 == SimpleTypesRef.UintTest   &&
                    test.ShortTest                                == SimpleTypesRef.ShortTest  &&
                    test.UshortTest                               == SimpleTypesRef.UshortTest &&
                    test.Long                                     == SimpleTypesRef.Long       &&
                    test.ULong                                    == SimpleTypesRef.ULong      &&
                    Math.Abs(test.Float  - SimpleTypesRef.Float)  < 0.0001f                    &&
                    Math.Abs(test.Double - SimpleTypesRef.Double) < 0.0001f) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match refrance");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }

            Assert.Fail();
        }

        [Test]
        public void SubTypes1()
        {
            var test = new SubTypeTest2();
            if (test.Unpack(SubType1TypesBytes.AsSpan()) > -1) {
                if (test.IntTest == 1 &&
                    AreTypesEqual(test.Sub)) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void SubTypes2()
        {
            var test = new SubTypeTest3();
            if (test.Unpack(SubType2TypesBytes.AsSpan()) > -1) {
                if (test.IntTest     == 1 &&
                    test.Sub.IntTest == 2 &&
                    AreTypesEqual(test.Sub.Sub)) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }

        public bool AreTypesEqual(SubType test)
        {
            return (test.Byte                                     == SimpleTypesRef.Byte       &&
                    test.Char                                     == SimpleTypesRef.Char       &&
                    test.IntTest                                  == SimpleTypesRef.IntTest    &&
                    test.UintTest                                 == SimpleTypesRef.UintTest   &&
                    test.ShortTest                                == SimpleTypesRef.ShortTest  &&
                    test.UshortTest                               == SimpleTypesRef.UshortTest &&
                    test.Long                                     == SimpleTypesRef.Long       &&
                    test.ULong                                    == SimpleTypesRef.ULong      &&
                    Math.Abs(test.Float  - SimpleTypesRef.Float)  < 0.0001f                    &&
                    Math.Abs(test.Double - SimpleTypesRef.Double) < 0.0001f);
        }
        
        [Test]
        public void Vector2Test()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
            var test = new Vector2Type();
            if (test.Unpack(data) > -1) {
                if (test.Vec2.X == 1f && test.Vec2.Y == 2f) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void Vector3Test()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
            var test = new Vector3Type();
            if (test.Unpack(data) > -1) {
                if (test.Vec3.X == 1f && test.Vec3.Y == 2f && test.Vec3.Z == 3f) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void Vector4Test()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
            var test = new Vector4Type();
            if (test.Unpack(data) > -1) {
                if (test.Vec4.X == 1f && test.Vec4.Y == 2f && test.Vec4.Z == 3f && test.Vec4.W == 4f) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void QuatTest()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
            var test = new QuatType();
            if (test.Unpack(data) > -1) {
                if (test.Quat.X == 1f && test.Quat.Y == 2f && test.Quat.Z == 3f && test.Quat.W == 4f) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
    }
}