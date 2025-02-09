using System;
using System.Reflection;
using Aero.Gen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aero.TestCases
{
    public partial class TestCase1Main
    {
        public void AeroTest2() {}
    }
    
    class Program
    {
        private static Compilation InputCompilation = CreateCompilation(@"
using System.Net.Security;
using Aero.Gen.Attributes;
using System.Numerics;
using Aero.Gen;

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

    [Aero(AeroGenTypes.View)]
    public partial class ViewTypeTest
    {
        [AeroString]
        public string Name;

        public int Id;

        public TestSubDataOne? TestBlock;

        public Vector3 Position;
        public int?    Number;
        public int?    Number1;
        public int?    Number2;
        public int?    Number3;
        public int?    Number4;
        public int?    Number5;
        public int?    Number6;
        public int?    Number7;
        public int?    Number8;
        public int?    Number9;
        public int?    Number10;
    }

    [AeroBlock]
    public struct TestSubDataOne
    {
        public byte   Byte;
        public char   Char;
        public int    IntTest;
        public uint   UintTest;

        [AeroArray(5)]
        public int[] ArrayTest;
    }

    [AeroBlock]
    public struct TestSubDataTwo
    {
        public byte   Byte;
        public char   Char;
        public int    IntTest;
        public uint   UintTest;
        public TestSubDataOne SubDataTwo;
    }

    [AeroBlock]
    public struct ArrayInStruct
    {
        [AeroArray(100)]
        public byte[] Arr;
    }

    public class Test2
    {
        [Aero]
        public partial class TestCase1
        {
    
            public ArrayInStruct ArrayInStructTest;
    
            [AeroArray(-4)]
            public TestSubDataOne[] ArrayTestTest;
    
            [AeroArray(4)]
            [AeroString(9)] public string[] TestString;
    
            [AeroArray(2)]
            public Vector2[] Vector2Test;
    
            public Vector2 Vec2;
    
            public TestSubDataTwo IntArray4;
    
            [AeroArray(typeof(int))]
            public TestSubDataTwo[] IntArray4;
    
            public TestFlags Flags;
    
            public byte   Byte;
            public char   Char;
            public int    IntTest;
            public uint   UintTest;
            public short  ShortTest;
            public ushort UshortTest;
            public long   Long;
            public ulong  ULong;
            public float  Float;
            
            [AeroIf(""IntTest"", -100)]
            public double Double;
            
            [AeroIf(nameof(IntTest), 100)]
            [AeroIf(nameof(IntTest), 200)]
            [AeroArray(nameof(Byte))]
            public int[] IntArray;
    
            [AeroIf(nameof(Byte), AeroIfAttribute.Ops.NotEqual, 0.5f, 1.0f)]
            [AeroArray(2)]
            public int[] IntArray2;
    
            //[AeroArray(typeof(int))]
            //public int[] IntArray3;
    
            public TestSubDataOne SubDataOne;
            public TestSubDataTwo SubData2;
    
            [AeroString(20)]
            public string TestString;
            
            [AeroString(nameof(Byte))]
            public string TestString2;
            
            [AeroString(typeof(int))]
            public string TestString3;
    
            public TestCase1()
            {
                
            }
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
    public partial class GssMsgTest6
    {
        
    }

    [Aero(AeroGenTypes.View)]
    public partial class Outpost_ObserverView
    {

        private uint OutpostName;

        private Vector3 Position;


        private uint LevelBandId;
        private byte  SinUnlockIndex;
        private int   TeleportCost;
        private float Progress; // Dynamic_00


        private byte FactionId;         // Dynamic_01
        private byte   Team;            // Dynamic_02
        private byte   UnderAttack;     // Dynamic_03
        private byte   OutpostType;     // Dynamic_04
        private uint   PossibleBuffsId; // Dynamic_05
        private byte   PowerLevel;      // Dynamic_06
        private ushort MWCurrent;       // Dynamic_07
        private ushort MWMax;           // Dynamic_08
        private uint   MapMarkerTypeId; // Dynamic_09
        private float  Radius;          // Dynamic_10

        [AeroArray(4)]
        private byte[] Dynamic_11;

        [AeroNullable] private uint NearbyResourceItems_0;
        [AeroNullable] private uint NearbyResourceItems_1;
        [AeroNullable] private uint NearbyResourceItems_2;
        [AeroNullable] private uint NearbyResourceItems_3;
        [AeroNullable] private uint NearbyResourceItems_4;
        [AeroNullable] private uint NearbyResourceItems_5;
        [AeroNullable] private uint NearbyResourceItems_6;
        [AeroNullable] private uint NearbyResourceItems_7;
        [AeroNullable] private uint NearbyResourceItems_8;
        [AeroNullable] private uint NearbyResourceItems_9;
        [AeroNullable] private uint NearbyResourceItems_10;
        [AeroNullable] private uint NearbyResourceItems_11;
        [AeroNullable] private uint NearbyResourceItems_12;
        [AeroNullable] private uint NearbyResourceItems_13;
        [AeroNullable] private uint NearbyResourceItems_14;
        [AeroNullable] private uint NearbyResourceItems_15;

        private ScopeBubbleInfoData ScopeBubbleInfo;
    }

    [AeroBlock]
    public struct ScopeBubbleInfoData
    {
        // Don't know how this works but its used everywhere so keeping it in a struct
        [AeroArray(8)]
        public byte[] Unk;
    }

}");

        public enum BitfieldMask : ulong
        {
            CinematicCamera       = 1UL << 0,
            PersonalFactionStance = 1UL << 1,

        }

        static void Main(string[] args)
        {
            AeroGenerator   generator = new AeroGenerator();
            GeneratorDriver driver    = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(InputCompilation, out var outputCompilation, out var diagnostics);

            foreach (var diag in diagnostics) {
                Console.WriteLine(diag);
            }

            //var data = new byte[] { 0x02, 0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF }.AsSpan();
            /*var data = new byte[10000000];
            new Random().NextBytes(data);
            var test = new TestCase1Main();
            
            try {
                test.Unpack(data);
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
            
            //test.GetPackedSize();

            foreach (var readLog in test.GetDiagReadLogs()) {
                //Console.WriteLine($"{readLog.Item1}, {readLog.Item2}, {readLog.Item3}, {readLog.Item4}, {readLog.Item5}");
                Console.WriteLine(readLog.ToString());
            }
            */

            var buffer = new Span<byte>();
            //test.Pack(buffer);

            /*foreach (var log in test.DiagLogs) {
                Console.WriteLine(log);
            }*/

            //var msgHander1 = AeroRouting.GetNewMessageHandler(AeroMessageIdAttribute.MsgType.Matrix, AeroMessageIdAttribute.MsgSrc.Command, 2);
        }
        
        private static Compilation CreateCompilation(string source)
            => CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    }
}