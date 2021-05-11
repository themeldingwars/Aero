using System;
using Aero.Gen.Attributes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

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

    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RuntimeMoniker.CoreRt50, launchCount: 1, warmupCount: 5, targetCount: 10)]
    [MemoryDiagnoser]
    [JsonExporterAttribute.Full]
    [JsonExporterAttribute.FullCompressed]
    public class Reading
    {
        private byte[]                TestData1;
        private SimpleTypes           SimpleTypesReader = new SimpleTypes();
        private SubTypeTest2          SubTypesReader    = new SubTypeTest2();
        private BenchmarkArrayReading ArrayReader       = new BenchmarkArrayReading();

        [GlobalSetup]
        public void Setup()
        {
            TestData1 = new byte[10000];
            new Random(42).NextBytes(TestData1);
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
        public int ArraysRead()
        {
            return ArrayReader.Unpack(TestData1.AsSpan());
        }
    }
}