using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Aero.Gen.Attributes;

namespace Aero.TestCases
{
    [AeroBlock]
    public struct TestSubDataOne
    {
        public byte Byte;
        public char Char;
        public int  IntTest;
        public uint UintTest;
    }
    
    [AeroBlock]
    public struct TestSubDataTwo
    {
        public byte           Byte;
        public TestSubDataOne SubData;
    }
    
    [Aero]
    public partial class TestCase1Main
    {
        /*public byte   Byte;
        public char   Char;
        public int    IntTest;
        public uint   UintTest;
        public short  ShortTest;
        public ushort UshortTest;
        public long   Long;
        public ulong  ULong;
        public float  Float;
        [AeroIf("IntTest", -100)]
        public double Double;
        [AeroIf("Byte", 0)]
        public double Double2;*/
        
        public byte Byte;
        
        [AeroIf("Byte", 0)]
        [AeroArray(typeof(int))]
        public int[] TestArr;

        [AeroArray(2)]
        public int[] TestArr2;
        
        [AeroArray(nameof(Byte))]
        public int[] TestArr3;
        
        [AeroArray(2)]
        public TestSubDataOne[] TestArr4;

        [AeroIf("Byte", AeroIfAttribute.Ops.Equal, 0.5f, 1.0f)]
        public TestSubDataOne TestSubData;
        public TestSubDataTwo TestSubData2;

        public TestCase1Main()
        {
            
        }

        public void TestRead(ReadOnlySpan<byte> data)
        {
            int offset = 0;
            //Byte    = data[0];
            //Char    = (char)data[0];
            //IntTest = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));

            TestSubData.Byte          = 0;
            TestSubData2.SubData.Byte = 0;
        }
    }
}