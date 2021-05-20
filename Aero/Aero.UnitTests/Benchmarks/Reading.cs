using System;
using System.IO;
using System.Numerics;
using Aero.Gen.Attributes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Aero.UnitTests.Benchmarks
{
    [Aero]
    public partial class SimpleTypesBenchmark
    {
        public byte       Byte;
        public char       Char;
        public int        IntTest;
        public uint       UintTest;
        public short      ShortTest;
        public ushort     UshortTest;
        public long       Long;
        public ulong      ULong;
        public float      Float;
        public double     Double;
        public Vector2    Vec2;
        public Vector3    Vec3;
        public Vector4    Vec4;
        public Quaternion Quat;
    }

    [Aero]
    public partial class BenchmarkArrayReading
    {
        [AeroArray(100)] public byte[]    ByteArray;
        [AeroArray(100)] public int[]     IntArray;
        [AeroArray(100)] public float[]   FloatArray;
        [AeroArray(100)] public SubType[] SubTypeArray;
    }

    [Aero]
    public partial class BenchmarkByteArrayReading
    {
        [AeroArray(100)] public byte[] ByteArray;
    }

    [Aero]
    public partial class BenchmarkIntArrayReading
    {
        [AeroArray(100)] public int[] IntArray;
    }

    [Aero]
    public partial class BenchmarkSubArrayReading
    {
        [AeroArray(100)] public SubType[] SubTypeArray;
    }

    [Aero]
    public partial class BenchmarkVector2Array2Reading
    {
        [AeroArray(2)] public Vector2[] Vec2Array;
    }

    [Aero]
    public partial class BenchmarkPackedSizeComplexTest
    {
        public                 int   TestInt;
        [AeroArray(10)] public int[] TestIntArray;

        [AeroIf(nameof(TestInt), 1)] public int TestIfValue;

        [AeroArray(20)] public SubType[] TestSubType;
    }

    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RuntimeMoniker.NetCoreApp50, launchCount: 1, warmupCount: 5, targetCount: 10)]
    [MemoryDiagnoser]
    [JsonExporterAttribute.Full]
    [JsonExporterAttribute.FullCompressed]
    public class Reading
    {
        private byte[]               TestData1;
        private byte[]               TestDataWrite;
        private SimpleTypesBenchmark SimpleTypesReader = new SimpleTypesBenchmark();
        private SubTypeTest2         SubTypesReader    = new SubTypeTest2();

        private BenchmarkArrayReading ArrayReader = new BenchmarkArrayReading
        {
            IntArray = new[] {1, 2, 3, 4}
        };

        private BenchmarkByteArrayReading ArrayReader2 = new BenchmarkByteArrayReading();

        private BenchmarkIntArrayReading ArrayReader3 = new BenchmarkIntArrayReading
        {
            IntArray = new[] {1, 2, 3, 4}
        };

        private BenchmarkSubArrayReading      ArrayReader4        = new BenchmarkSubArrayReading();
        private BenchmarkVector2Array2Reading Vector2Array2Reader = new BenchmarkVector2Array2Reading();

        private BenchmarkPackedSizeComplexTest PackedSizeComplexTestInstance = new BenchmarkPackedSizeComplexTest
        {
            TestInt      = 1,
            TestIntArray = new[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9},
            TestIfValue  = 2,
            TestSubType = new[]
            {
                new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(),
                new SubType(), new SubType(), new SubType(),
                new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(),
                new SubType(), new SubType(), new SubType()
            }
        };

        private StringTest1 StringTest1 = new StringTest1 {TestString = "testyay:>"};
        private StringTest3 StringTest2 = new StringTest3 {TestString = "testyay:>"};
        private StringTest4 StringTest3 = new StringTest4 {TestString = "testyay:>"};

        [GlobalSetup]
        public void Setup()
        {
            TestData1     = new byte[10000];
            TestDataWrite = new byte[10000];
            new Random(42).NextBytes(TestData1);

            ArrayReader2.ByteArray = new byte[100];
            new Random(42).NextBytes(ArrayReader2.ByteArray);

            ArrayReader3.IntArray = new int[100];
            for (int i = 0; i < 100; i++) {
                ArrayReader3.IntArray[i] = i;
            }

            ArrayReader4.SubTypeArray = new SubType[100];
            for (int i = 0; i < 100; i++) {
                ArrayReader4.SubTypeArray[i] = new SubType()
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
                    Double     = 2.5d
                };
            }

            Vector2Array2Reader.Vec2Array = new Vector2[2];
            for (int i = 0; i < 2; i++) {
                Vector2Array2Reader.Vec2Array[i] = new Vector2(1, 2);
            }

            ArrayReader.ByteArray    = new byte[100];
            ArrayReader.FloatArray   = new float[100];
            ArrayReader.IntArray     = new int[100];
            ArrayReader.SubTypeArray = new SubType[100];
            for (int i = 0; i < 100; i++) {
                ArrayReader.ByteArray[i]  = 0;
                ArrayReader.FloatArray[i] = i;
                ArrayReader.IntArray[i]   = i;
                ArrayReader.SubTypeArray[i] = new SubType
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
                    Double     = 2.5d
                };
            }

            ArrayReader4.SubTypeArray = new SubType[100];
            for (int i = 0; i < 100; i++) {
                ArrayReader4.SubTypeArray[i] = new SubType
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
                    Double     = 2.5d
                };
            }
        }

        [Benchmark]
        public int BinaryReaderBaseLine()
        {
            using (BinaryReader br = new BinaryReader(new MemoryStream(TestData1))) {
                SimpleTypesReader.Byte       = br.ReadByte();
                SimpleTypesReader.Char       = br.ReadChar();
                SimpleTypesReader.IntTest    = br.ReadInt32();
                SimpleTypesReader.UintTest   = br.ReadUInt32();
                SimpleTypesReader.ShortTest  = br.ReadInt16();
                SimpleTypesReader.UshortTest = br.ReadUInt16();
                SimpleTypesReader.Long       = br.ReadInt64();
                SimpleTypesReader.ULong      = br.ReadUInt64();
                SimpleTypesReader.Float      = br.ReadSingle();
                SimpleTypesReader.Double     = br.ReadDouble();
                SimpleTypesReader.Vec2 = new Vector2
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle()
                };
                SimpleTypesReader.Vec3 = new Vector3
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle()
                };
                SimpleTypesReader.Vec4 = new Vector4
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                    W = br.ReadSingle()
                };
                SimpleTypesReader.Quat = new Quaternion()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                    W = br.ReadSingle()
                };
            }

            return 1;
        }

        [Benchmark]
        public int BinaryWriterBaseLine()
        {
            using (BinaryWriter br = new BinaryWriter(new MemoryStream())) {
                br.Write(SimpleTypesReader.Byte);
                br.Write(SimpleTypesReader.Char);
                br.Write(SimpleTypesReader.IntTest);
                br.Write(SimpleTypesReader.UintTest);
                br.Write(SimpleTypesReader.ShortTest);
                br.Write(SimpleTypesReader.UshortTest);
                br.Write(SimpleTypesReader.Long);
                br.Write(SimpleTypesReader.ULong);
                br.Write(SimpleTypesReader.Float);
                br.Write(SimpleTypesReader.Double);

                br.Write(SimpleTypesReader.Vec2.X);
                br.Write(SimpleTypesReader.Vec2.Y);

                br.Write(SimpleTypesReader.Vec3.X);
                br.Write(SimpleTypesReader.Vec3.Y);
                br.Write(SimpleTypesReader.Vec3.Z);

                br.Write(SimpleTypesReader.Vec4.X);
                br.Write(SimpleTypesReader.Vec4.Y);
                br.Write(SimpleTypesReader.Vec4.Z);
                br.Write(SimpleTypesReader.Vec4.W);

                br.Write(SimpleTypesReader.Quat.X);
                br.Write(SimpleTypesReader.Quat.Y);
                br.Write(SimpleTypesReader.Quat.Z);
                br.Write(SimpleTypesReader.Quat.W);
            }

            return 1;
        }

        [Benchmark]
        public int SimpleTypesRead()
        {
            return SimpleTypesReader.Unpack(TestData1.AsSpan());
        }

        [Benchmark]
        public int SubTypesRead()
        {
            return SubTypesReader.Unpack(TestData1.AsSpan());
        }

        [Benchmark]
        public int ArraysCombinedRead()
        {
            return ArrayReader.Unpack(TestData1.AsSpan());
        }

        [Benchmark]
        public int ByteArrayRead()
        {
            return ArrayReader2.Unpack(TestData1.AsSpan());
        }

        [Benchmark]
        public int IntArrayRead()
        {
            return ArrayReader3.Unpack(TestData1.AsSpan());
        }

        [Benchmark]
        public int BlockArrayRead()
        {
            return ArrayReader4.Unpack(TestData1.AsSpan());
        }

    #region String Benchmarks

        [Benchmark]
        public int FixedLengthString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x01, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x02};
            return StringTest1.Unpack(data);
        }

        [Benchmark]
        public int PrefixedLengthString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x09, 0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x02};
            return StringTest2.Unpack(data);
        }

        [Benchmark]
        public int NullTerminatedLengthString()
        {
            ReadOnlySpan<byte> data = new byte[] {0x74, 0x65, 0x73, 0x74, 0x79, 0x61, 0x79, 0x3A, 0x3E, 0x00, 0x02};
            return StringTest3.Unpack(data);
        }

    #endregion

        [Benchmark]
        public int Vector2Array2Read()
        {
            ReadOnlySpan<byte> data = new byte[]
                {0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40};
            return Vector2Array2Reader.Unpack(data);
        }

        [Benchmark]
        public int GetPackedLengthSimple()
        {
            return Tests.SimpleTypesRef.GetPackedSize();
        }

        [Benchmark]
        public int GetPackedLengthComplex()
        {
            return PackedSizeComplexTestInstance.GetPackedSize();
        }

    #region Pack Benchmarks

        [Benchmark]
        public int PackSimple()
        {
            return Tests.SimpleTypesRef.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackSubTypesSimple()
        {
            return SubTypesReader.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackComplex()
        {
            return PackedSizeComplexTestInstance.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackArraysCombined()
        {
            return ArrayReader.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackByteArray()
        {
            return ArrayReader2.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackIntArray()
        {
            return ArrayReader3.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackBlockArray()
        {
            return ArrayReader4.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackFixedLengthString()
        {
            return StringTest1.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackPrefixedLengthString()
        {
            return StringTest2.Pack(TestDataWrite.AsSpan());
        }

        [Benchmark]
        public int PackNullTerminatedLengthString()
        {
            return StringTest3.Pack(TestDataWrite.AsSpan());
        }

    #endregion
    }
}