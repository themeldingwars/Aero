using System;
using Aero.Gen.Attributes;
using NuGet.Frameworks;
using NUnit.Framework;
using static Aero.Gen.Attributes.AeroIfAttribute;

namespace Aero.UnitTests
{
    [Aero]
    public partial class IfTest1
    {
        public int IfValue;
        
        [AeroIf(nameof(IfValue), 1)]
        public int ToReadIfValueMatches = 0;
    }
    
    [Aero]
    public partial class IfTest2
    {
        public int IfValue;
        public int IfValue2;
        
        [AeroIf(nameof(IfValue), 1)]
        [AeroIf(nameof(IfValue2), 2)]
        public int ToReadIfValueMatches = 0;
    }
    
    [Flags]
    public enum TestFlags : byte
    {
        None  = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag3 = 4,
        Flag4  = 8
    }
    
    [Aero]
    public partial class IfTest3
    {
        public TestFlags TestFlags;
        
        [AeroIf(nameof(TestFlags), Ops.HasFlag, TestFlags.Flag1)]
        public byte      ByteToRead;
    }
    
    [Aero]
    public partial class IfTest4
    {
        public TestFlags TestFlags;
        
        [AeroIf(nameof(TestFlags), Ops.HasFlag, TestFlags.Flag1, TestFlags.Flag2)]
        public byte ByteToRead;
    }
    
    [Aero]
    public partial class IfTest5
    {
        public TestFlags TestFlags;
        
        [AeroIf(nameof(TestFlags), Ops.DoesntHaveFlag, TestFlags.Flag1)]
        public byte ByteToRead;
    }
    
    [Aero]
    public partial class IfTest6
    {
        public byte IfValue;
        
        [AeroIf(nameof(IfValue), 1)]
        public SubTypeSimple SubTypeToRead;
    }
    
    public class IfTests
    {
        private static byte[] IfTest1_Read = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 };
        private static byte[] IfTest1_DontRead = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 };
        
        private static byte[] IfTest2_Read     = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 };
        private static byte[] IfTest2_DontRead = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 };
        
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void OneIfShouldReadSecondValue()
        {
            var test = new IfTest1();
            if (test.Unpack(IfTest1_Read.AsSpan()) > -1) {
                if (test.ToReadIfValueMatches == 2) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void OneIfShouldntReadSecondValue()
        {
            var test = new IfTest1();
            if (test.Unpack(IfTest1_DontRead.AsSpan()) > -1) {
                if (test.ToReadIfValueMatches == 0) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void TwoIfShouldReadSecondValue()
        {
            var test = new IfTest2();
            if (test.Unpack(IfTest2_Read.AsSpan()) > -1) {
                if (test.ToReadIfValueMatches == 3) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void TwoIfShouldntReadSecondValue()
        {
            var test = new IfTest2();
            if (test.Unpack(IfTest2_DontRead.AsSpan()) > -1) {
                if (test.ToReadIfValueMatches == 0) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void EnumHasFlagsTest1()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x01, 0x2 };
            var                test = new IfTest3();
            if (test.Unpack(data) > -1) {
                if (test.ByteToRead == 2) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void EnumHasFlagsTest2()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x02, 0x2 };
            var                test = new IfTest3();
            if (test.Unpack(data) > -1) {
                if (test.ByteToRead == 0) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void EnumHasFlagsTest3()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x03, 0x2 };
            var                test = new IfTest4();
            if (test.Unpack(data) > -1) {
                if (test.ByteToRead == 2) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void EnumDoesntHasFlagsTest()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x02, 0x2 };
            var                test = new IfTest5();
            if (test.Unpack(data) > -1) {
                if (test.ByteToRead == 2) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
        
        [Test]
        public void SubTypeIfTest()
        {
            ReadOnlySpan<byte> data = new byte[] { 0x01, 0x01, 0x00, 0x00, 0x00 };
            var                test = new IfTest6();
            if (test.Unpack(data) > -1) {
                if (test.SubTypeToRead.IntTest == 1) {
                    Assert.Pass();
                }
                
                Assert.Fail("Value wasn't read");
            }
            else {
                Assert.Fail("Didn't read all fields");
            }
        }
    }
}