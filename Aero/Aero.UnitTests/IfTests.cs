using System;
using Aero.Gen.Attributes;
using NuGet.Frameworks;
using NUnit.Framework;

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
    }
}