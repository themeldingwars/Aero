using BenchmarkDotNet.Running;

namespace Aero.UnitTests.Benchmarks
{
    public class Runner
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Reading>();
        }
    }
}