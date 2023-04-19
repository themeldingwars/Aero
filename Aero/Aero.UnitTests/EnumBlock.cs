using System;
using Aero.Gen.Attributes;
using NuGet.Frameworks;
using NUnit.Framework;
using System.Numerics;

namespace Aero.UnitTests
{
    [Aero]
    public partial class EnumBlockArrayTest
    {
       [AeroArray(typeof(byte))]
       public AaaaBlockWithEnum[] ArrayOfBlockWithEnum;
    }

    [AeroBlock]
    public struct AaaaBlockWithEnum
    {
        public uint JustForShow;
        public MyPrettyEnum Flags;
    }

    [Flags]
    public enum MyPrettyEnum : byte
    {
        Key    = 1 << 1,
    }


    public class EnumBlockSizeTests
    {
        public static EnumBlockArrayTest EnumBlockArrayTestRef = new EnumBlockArrayTest()
        {
            ArrayOfBlockWithEnum = new AaaaBlockWithEnum[] {
                new AaaaBlockWithEnum {
                    JustForShow = 1,
                    Flags = (MyPrettyEnum) 3
                },
            }
        };

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ArrayOfBlockWithEnumUnpack()
        {
            ReadOnlySpan<byte> data = new byte[] {0x01, 0x01, 0x00, 0x00, 0x00, 0x3};
            var                test = new EnumBlockArrayTest();
            if (test.Unpack(data) > 0) {
                if (test.ArrayOfBlockWithEnum.Length == 1
                    && test.ArrayOfBlockWithEnum[0].JustForShow == 1
                    && test.ArrayOfBlockWithEnum[0].Flags == (MyPrettyEnum) 3) {
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
        public void ArrayOfBlockWithEnumGetPackedLength()
        {
            var packedSize = EnumBlockArrayTestRef.GetPackedSize();
            if (packedSize == 6) {
                Assert.Pass();
            }
            else {
                Assert.Fail($"Expected packedSize 6 but got {packedSize}");
            }
        }
    }
}