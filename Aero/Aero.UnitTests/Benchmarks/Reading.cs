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
        public int   TestInt;
        [AeroArray(10)]
        public int[] TestIntArray;
        
        [AeroIf(nameof(TestInt), 1)]
        public int TestIfValue;

        [AeroArray(20)]
        public SubType[] TestSubType;
    }

    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RuntimeMoniker.NetCoreApp50, launchCount: 1, warmupCount: 5, targetCount: 10)]
    [MemoryDiagnoser]
    [JsonExporterAttribute.Full]
    [JsonExporterAttribute.FullCompressed]
    public class Reading
    {
        private byte[]                        TestData1;
        private SimpleTypes                   SimpleTypesReader   = new SimpleTypes();
        private SubTypeTest2                  SubTypesReader      = new SubTypeTest2();
        private BenchmarkArrayReading         ArrayReader         = new BenchmarkArrayReading();
        private BenchmarkByteArrayReading     ArrayReader2        = new BenchmarkByteArrayReading();
        private BenchmarkIntArrayReading      ArrayReader3        = new BenchmarkIntArrayReading();
        private BenchmarkSubArrayReading      ArrayReader4        = new BenchmarkSubArrayReading();
        private BenchmarkVector2Array2Reading Vector2Array2Reader = new BenchmarkVector2Array2Reading();

        private BenchmarkPackedSizeComplexTest PackedSizeComplexTestInstance = new BenchmarkPackedSizeComplexTest
        {
            TestInt = 1,
            TestIntArray = new []{ 0, 1, 2, 3,4, 5, 6, 7, 8, 9 },
            TestIfValue = 2,
            TestSubType = new []
            {
                new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(),
                new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType(), new SubType()
            }
        };

        private StringTest1 StringTest1 = new StringTest1();
        private StringTest3 StringTest2 = new StringTest3();
        private StringTest4 StringTest3 = new StringTest4();

        [GlobalSetup]
        public void Setup()
        {
            TestData1 = new byte[10000];
            new Random(42).NextBytes(TestData1);
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
            ReadOnlySpan<byte> data = new byte[] { 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x80, 0x40  };
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
    }
}