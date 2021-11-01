using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aero.Gen;
using Aero.Gen.Attributes;
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

            var data = new byte[] { 0x02, 0x01, 0x41, 0x9C, 0xFF, 0xFF, 0xFF }.AsSpan();
            var test = new TestCase1Main();
            //test.Unpack(data);
            //test.GetPackedSize();

            var buffer = new Span<byte>();
            //test.Pack(buffer);

            /*foreach (var log in test.DiagLogs) {
                Console.WriteLine(log);
            }*/

            var msgHander1 = AeroRouting.GetNewMessageHandler(AeroMessageIdAttribute.MsgType.Matrix, AeroMessageIdAttribute.MsgSrc.Command, 2);

        }
        
        private static Compilation CreateCompilation(string source)
            => CSharpCompilation.Create("compilation",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    }
}