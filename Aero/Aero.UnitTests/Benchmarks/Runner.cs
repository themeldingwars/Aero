using BenchmarkDotNet.Running;
using NUnit.Framework;

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