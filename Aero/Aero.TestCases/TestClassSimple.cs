using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
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
        public                           byte   Byte;
        public                           char   Char;
        public                           int    IntTest;
        public                           uint   UintTest;
        public                           short  ShortTest;
        public                           ushort UshortTest;
        public                           long   Long;
        public                           ulong  ULong;
        public                           float  Float;
        [AeroIf("IntTest", -100)] public double Double;
        [AeroIf("Byte", 0)]       public double Double2;

        public Vector2 Vec2;

        public TestFlags Bytea;

        //public byte Byte;

        [AeroIf("Byte", 0)] [AeroIf("Byte", 1)] [AeroArray(typeof(int))]
        public int[] TestArr;

        [AeroIf("Bytea", Ops.HasFlag, TestFlags.Flag2, TestFlags.Flag3)] [AeroArray(2)]
        public int[] TestArr2;

        [AeroArray(nameof(Byte))] public int[] TestArr3;

        [AeroArray(2)] public TestSubDataOne[] TestArr4;

        //[AeroIf("Byte", AeroIfAttribute.Ops.Equal, 0.5f, 1.0f)]
        public TestSubDataOne TestSubData;
        public TestSubDataTwo TestSubData2;

        [AeroString(20)] public string TestString;

        [AeroString(nameof(Byte))] public string TestString2;

        [AeroString(typeof(int))] public string TestString3;

        [AeroString] public string TestString4;

        public TestSubDataTwo TestSubData3;

        public byte Byte2;

        [AeroIf(nameof(Byte), 1)] public TestSubDataOne TestSubData4;

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
    [AeroMessageId(MsgType.GSS, MsgSrc.Both, 1, 1)]
    public partial class GssBothTest1
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Command, 1, 2)]
    public partial class GssMsgCmdTest2
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, 1, 3)]
    public partial class GssMsgTest3
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Both, 2, 1)]
    public partial class GssBothTest4
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Command, 2, 2)]
    public partial class GssMsgCmdTest5
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, 2, 3)]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, 3, 3)]
    public partial class GssMsgTest6
    {
    }

    [Aero(AeroGenTypes.View)]
    public partial class ViewTypeTest
    {
        [AeroString] private string Name;

        private int Id;

        //private TestSubDataOne TestBlock;

        //public Vector3 Position;
        [AeroNullable] public int Number;
        [AeroNullable] public int Number1;
        [AeroNullable] public int Number2;
        [AeroNullable] public int Number3;
        [AeroNullable] public int Number4;
        [AeroNullable] public int Number5;
        [AeroNullable] public int Number6;
        [AeroNullable] public int Number7;
        [AeroNullable] public int Number8;
        [AeroNullable] public int Number9;
        [AeroNullable] public int Number10;
        [AeroNullable] public int Number11;
        [AeroNullable] public int Number12;
        [AeroNullable] public int Number13;
        [AeroNullable] public int Number14;
        [AeroNullable] public int Number15;
        [AeroNullable] public int Number16;
        [AeroNullable] public int Number17;
        [AeroNullable] public int Number18;
        [AeroNullable] public int Number19;
        [AeroNullable] public int Number20;
        [AeroNullable] public int Number21;
        [AeroNullable] public int Number22;
        //public int?    Number23;
        //public int?    Number24;
        //public int?    Number25;
        //public int?    Number26;
        //public int?    Number27;
        //public int?    Number28;
        //public int?    Number29;
        /*public int?    Number30;
        public int?    Number31;
        public int?    Number32;
        public int?    Number33;
        public int?    Number34;
        public int?    Number35;
        public int?    Number36;
        public int?    Number37;
        public int?    Number38;
        public int?    Number39;
        public int?    Number40;
        public int?    Number41;
        public int?    Number42;
        public int?    Number43;
        public int?    Number44;
        public int?    Number45;
        public int?    Number46;
        public int?    Number47;
        public int?    Number48;
        public int?    Number49;
        public int?    Number50;
        public int?    Number51;
        public int?    Number52;
        public int?    Number53;
        public int?    Number54;
        public int?    Number55;
        public int?    Number56;
        public int?    Number57;
        public int?    Number58;
        public int?    Number59;
        public int?    Number60;
        public int?    Number61;
        public int?    Number62;
        public int?    Number63;
        public int?    Number64;
        public int?    Number65;
        public int?    Number66;
        public int?    Number67;
        public int?    Number68;
        public int?    Number69;
        public int?    Number70;
        public int?    Number71;
        public int?    Number72;
        public int?    Number73;
        public int?    Number74;
        public int?    Number75;
        public int?    Number76;
        public int?    Number77;
        public int?    Number78;
        public int?    Number79;
        public int?    Number80;
        public int?    Number81;
        public int?    Number82;
        public int?    Number83;
        public int?    Number84;
        public int?    Number85;
        public int?    Number86;
        public int?    Number87;
        public int?    Number88;
        public int?    Number89;
        public int?    Number90;
        public int?    Number91;
        public int?    Number92;
        public int?    Number93;
        public int?    Number94;
        public int?    Number95;
        public int?    Number96;
        public int?    Number97;
        public int?    Number98;
        public int?    Number99;
        public int?    Number100;*/
        /*public int?    Number101;
        public int?    Number102;
        public int?    Number103;
        public int?    Number104;
        public int?    Number105;
        public int?    Number106;
        public int?    Number107;
        public int?    Number108;
        public int?    Number109;
        public int?    Number110;
        public int?    Number111;
        public int?    Number112;
        public int?    Number113;
        public int?    Number114;
        public int?    Number115;
        public int?    Number116;
        public int?    Number117;
        public int?    Number118;
        public int?    Number119;
        public int?    Number120;
        public int?    Number121;
        public int?    Number122;
        public int?    Number123;
        public int?    Number124;
        public int?    Number125;
        public int?    Number126;
        public int?    Number127;
        public int?    Number128;
        public int?    Number129;
        public int?    Number130;
        public int?    Number131;
        public int?    Number132;
        public int?    Number133;
        public int?    Number134;
        public int?    Number135;
        public int?    Number136;
        public int?    Number137;
        public int?    Number138;
        public int?    Number139;
        public int?    Number140;
        public int?    Number141;
        public int?    Number142;
        public int?    Number143;
        public int?    Number144;
        public int?    Number145;
        public int?    Number146;
        public int?    Number147;
        public int?    Number148;
        public int?    Number149;
        public int?    Number150;
        public int?    Number151;
        public int?    Number152;
        public int?    Number153;
        public int?    Number154;
        public int?    Number155;
        public int?    Number156;
        public int?    Number157;
        public int?    Number158;
        public int?    Number159;
        public int?    Number160;
        public int?    Number161;
        public int?    Number162;
        public int?    Number163;
        public int?    Number164;
        public int?    Number165;
        public int?    Number166;
        public int?    Number167;
        public int?    Number168;
        public int?    Number169;
        public int?    Number170;
        public int?    Number171;
        public int?    Number172;
        public int?    Number173;
        public int?    Number174;
        public int?    Number175;
        public int?    Number176;
        public int?    Number177;
        public int?    Number178;
        public int?    Number179;
        public int?    Number180;
        public int?    Number181;
        public int?    Number182;
        public int?    Number183;
        public int?    Number184;
        public int?    Number185;
        public int?    Number186;
        public int?    Number187;
        public int?    Number188;
        public int?    Number189;
        public int?    Number190;
        public int?    Number191;
        public int?    Number192;
        public int?    Number193;
        public int?    Number194;
        public int?    Number195;
        public int?    Number196;
        public int?    Number197;
        public int?    Number198;
        public int?    Number199;*/
    }
}