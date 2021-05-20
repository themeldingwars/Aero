using System;
using System.Linq;
using System.Numerics;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class StringWriteTests
    {
        [AeroString(9)] public string FixedLenString;

        public                                 int    StringLen;
        [AeroString(nameof(StringLen))] public string RefLenString;

        [AeroString(typeof(int))] public string TypedLenString;

        [AeroString] public string NullTerminatedLenString;
    }

    [Aero]
    public partial class StringArrayWriteTests
    {
        [AeroArray(4)] [AeroString(9)] public string[] FixedLenString;

        [AeroArray(4)] [AeroString] public string[] NullTerminatedLenString;

        [AeroArray(4)] [AeroString(typeof(int))]
        public string[] TypedLenString;
    }

    [Aero]
    public partial class AdvancedTypesTests
    {
        public Vector2 Vec2;
        public Vector3 Vec3;
        public Vector4 Vec4;
        public Quaternion Quat;
    }

    [Aero]
    public partial class BlockArrayTests
    {
        [AeroArray(4)] public SubType[] ArrayBlocks;
    }

    public class WriteTests 
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void StringWriteTest()
        {
            var result = new byte[]
            {
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new StringWriteTests
            {
                FixedLenString          = "testyay:>",
                StringLen               = 9,
                RefLenString            = "testyay:>",
                TypedLenString          = "testyay:>",
                NullTerminatedLenString = "testyay:>"
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void StringArrayWriteTest()
        {
            var result = new byte[]
            {
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E,
                0x09, 0x00, 0x00, 0x00, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E
            };
            var bufferBytes = new byte[result.Length];
            var test = new StringArrayWriteTests
            {
                FixedLenString          = new[] {"testyay:>", "testyay:>", "testyay:>", "testyay:>"},
                NullTerminatedLenString = new[] {"testyay:>", "testyay:>", "testyay:>", "testyay:>"},
                TypedLenString          = new[] {"testyay:>", "testyay:>", "testyay:>", "testyay:>"}
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void SimpleTypesTest()
        {
            var result = new byte[]
            {
                0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
                0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
                0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40
            };
            var bufferBytes = new byte[result.Length];
            var test = new SimpleTypes()
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

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void AdvancedTypesTest()
        {
            
            var result = new byte[]
            {
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40,
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40,
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40,
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40
                
            };
            var bufferBytes = new byte[result.Length];
            var test = new AdvancedTypesTests
            {
                Vec2 = new Vector2(1, 2),
                Vec3 = new Vector3(1, 2, 3),
                Vec4 = new Vector4(1, 2, 3, 4),
                Quat = new Quaternion(1, 2, 3, 4)
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void FixedIntArrayTest()
        {
            var result = new byte[]
            {
                0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new IntArrayFixedTest
            {
                ArrayTest = new []{ 1, 2, 3, 4 }
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void RefIntArrayTest()
        {
            var result = new byte[]
            {
                0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new IntArrayRefLenTest
            {
                IntArrayRefLen = 4,
                ArrayTest = new []{ 1, 2, 3, 4 }
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void TypeIntArrayTest()
        {
            var result = new byte[]
            {
                0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new IntArrayTypeLenTest
            {
                ArrayTest = new []{ 1, 2, 3, 4 }
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void FixedBlockArrayTest()
        {
            var result = new byte[]
            {
                0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
                0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
                0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40,
                
                0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
                0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
                0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40,
                
                0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
                0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
                0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40,
                
                0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF, 0x64, 0x00, 0x00, 0x00, 0xCE, 0xFF, 0x32, 0x00, 0xC0, 0xBD,
                0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x42, 0x0F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9A, 0x99,
                0x99, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40
            };
            var bufferBytes = new byte[result.Length];
            var test = new BlockArrayTests
            {
                ArrayBlocks = new []
                {
                    new SubType()
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
                    },
                    new SubType()
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
                    },
                    new SubType()
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
                    },
                    new SubType()
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
                    }
                }
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void IfWriteShouldWriteTest()
        {
            var result = new byte[]
            {
                0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new IfTest1
            {
                IfValue = 1,
                ToReadIfValueMatches = 2
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
        
        [Test]
        public void IfWriteShouldntWriteTest()
        {
            var result = new byte[]
            {
                0x04, 0x00, 0x00, 0x00
            };
            var bufferBytes = new byte[result.Length];
            var test = new IfTest1
            {
                IfValue              = 4,
                ToReadIfValueMatches = 2
            };

            int bytesWritten = test.Pack(bufferBytes.AsSpan());
            if (bytesWritten == result.Length && bufferBytes.SequenceEqual(result)) {
                Assert.Pass();
            }

            Assert.Fail();
        }
    }
}