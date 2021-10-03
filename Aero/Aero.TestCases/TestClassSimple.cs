using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Aero.Gen;
using Aero.Gen.Attributes;
using static Aero.Gen.Attributes.AeroIfAttribute;
using static Aero.Gen.Attributes.AeroMessageIdAttribute;

namespace Aero.TestCases
{
    [Flags]
    public enum TestFlags : byte
    {
        Flag1,
        Flag2,
        Flag3,
        Flag4
    }
    

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
        public byte   Byte;
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
        public double Double2;

        public Vector2 Vec2;
        
        public TestFlags Bytea;
        
        //public byte Byte;
        
        [AeroIf("Byte", 0)]
        [AeroIf("Byte", 1)]
        [AeroArray(typeof(int))]
        public int[] TestArr;

        [AeroIf("Bytea", Ops.HasFlag, TestFlags.Flag2, TestFlags.Flag3)]
        [AeroArray(2)]
        public int[] TestArr2;
        
        [AeroArray(nameof(Byte))]
        public int[] TestArr3;
        
        [AeroArray(2)]
        public TestSubDataOne[] TestArr4;

        //[AeroIf("Byte", AeroIfAttribute.Ops.Equal, 0.5f, 1.0f)]
        public TestSubDataOne TestSubData;
        public TestSubDataTwo TestSubData2;
        
        [AeroString(20)]
        public string TestString;
        
        [AeroString(nameof(Byte))]
        public string TestString2;
        
        [AeroString(typeof(int))]
        public string TestString3;
        
        [AeroString]
        public string TestString4;
        
        public TestSubDataTwo TestSubData3;

        public byte Byte2;
        
        [AeroIf(nameof(Byte), 1)]
        public TestSubDataOne TestSubData4;
        
        public TestCase1Main()
        {
            
        }

        public void TestRead(ReadOnlySpan<byte> data)
        {
            int offset = 1;
            //Byte    = data[0];
            //Char    = (char)data[0];
            IntTest = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));

            //TestSubData.Byte          = 0;
            //TestSubData2.SubData.Byte = 0;

            TestFlags flagsTest = TestFlags.Flag2;
            if ((flagsTest & TestFlags.Flag2) == 0) {
                
            }

            if ((flagsTest & TestFlags.Flag2) != 0) {

            }

            //TestString = Encoding.UTF8.GetString(data);
            
            data.Slice(offset, data.Length - offset).IndexOf<byte>(0x00);

            var buffer = new Span<byte>();
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, UintTest);

            //Vec2 = MemoryMarshal.Read<Vector2>(data);

            //var strBytes = Encoding.ASCII.GetBytes(TestString).AsSpan();
            //strBytes.CopyTo(data.Slice(0, strBytes.Length));
        }
    }
    
    [Aero]
    [AeroMessageId(MsgType.Control, MsgSrc.Both, 1)]
    public partial class ControlMsgBothTest1
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Both, 1)]
    public partial class MatrixBothTest1
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Command, 2)]
    public partial class MatrixMsgCmdTest2
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, 3)]
    public partial class MatrixMsgTest3
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Both, 1)]
    public partial class GssBothTest1
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Command, 2)]
    public partial class GssMsgCmdTest2
    {
        
    }
    
    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, 3)]
    public partial class GssMsgTest3
    {
        
    }
}