using System;
using Aero.Gen.Attributes;
using NuGet.Frameworks;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class StringTest1
    {
        public                 byte   Leading;
        [AeroString(9)] public string TestString;
        public                 byte   Tailing;
    }

    [Aero]
    public partial class StringTest2
    {
        public                                  byte   StringSize;
        [AeroString(nameof(StringSize))] public string TestString;
        public                                  byte   Tailing;
    }

    [Aero]
    public partial class StringTest3
    {
        [AeroString(typeof(byte))] public string TestString;
        public                            byte   Tailing;
    }

    [Aero]
    public partial class StringTest4
    {
        [AeroString] public string TestString;
        public              byte   Tailing;
    }
    
    [Aero]
    public partial class StringTest5
    {
        [AeroString] public string TestString;
    }
    
    [Aero]
    public partial class StringTest6
    {
        [AeroArray(4)]
        [AeroString(9)] public string[] TestString;
    }
    
    [Aero]
    public partial class StringTest7
    {
        [AeroArray(4)]
        [AeroString] public string[] TestString;
    }
    
    [Aero]
    public partial class StringTest8
    {
        [AeroArray(typeof(byte))]
        [AeroString(typeof(byte))]public string[] TestString;
    }

    public class StringTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void FixedSizeString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x01, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x02};
            var                test = new StringTest1();
            if (test.Unpack(data) > 0) {
                if (test.Leading == 1 && test.TestString == "testyay:>" && test.Tailing == 2) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }

        [Test]
        public void RefedSizeString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x02};
            var                test = new StringTest2();
            if (test.Unpack(data) > 0) {
                if (test.TestString == "testyay:>" && test.Tailing == 2) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }

        [Test]
        public void TypedSizeString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x02};
            var                test = new StringTest3();
            if (test.Unpack(data) > 0) {
                if (test.TestString == "testyay:>" && test.Tailing == 2) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }

        [Test]
        public void NullTerminatedString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00, 0x02};
            var                test = new StringTest4();
            if (test.Unpack(data) > 0) {
                if (test.TestString == "testyay:>" && test.Tailing == 2) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }

        [Test]
        public void UntillEndOfSliceString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E};
            var                test = new StringTest5();
            if (test.Unpack(data) > 0) {
                if (test.TestString == "testyay:>") {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayOf4FIxedStrings()
        {
            ReadOnlySpan<byte> data = new byte[]
            {
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x31,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x32,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x33,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x34
            };
            var                test = new StringTest6();
            if (test.Unpack(data) > 0) {
                bool allPass = true;
                int  idx     = 1;
                foreach (var str in test.TestString) {
                    if (str != $"testyay {idx++}") {
                        allPass = false;
                        break;
                    }
                }
                if (allPass) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayOf4NullTerminatedStrings()
        {
            ReadOnlySpan<byte> data = new byte[]
            {
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x31, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x32, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x33, 0x00,
                0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x34, 0x00
            };
            var test = new StringTest7();
            if (test.Unpack(data) > 0) {
                bool allPass = true;
                int  idx     = 1;
                foreach (var str in test.TestString) {
                    if (str != $"testyay {idx++}") {
                        allPass = false;
                        break;
                    }
                }
                if (allPass) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }
        
        [Test]
        public void ArrayOfLengthPrefixedStrings()
        {
            ReadOnlySpan<byte> data = new byte[]
            {
                0x04,
                0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x31,
                0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x32,
                0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x33,
                0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x20, 0x34
            };
            var test = new StringTest8();
            if (test.Unpack(data) > 0) {
                bool allPass = true;
                int  idx     = 1;
                foreach (var str in test.TestString) {
                    if (str != $"testyay {idx++}") {
                        allPass = false;
                        break;
                    }
                }
                if (allPass) {
                    Assert.Pass();
                }
                else {
                    Assert.Fail("Values didn't match");
                }
            }
            else {
                Assert.Fail();
            }
        }
    }
}