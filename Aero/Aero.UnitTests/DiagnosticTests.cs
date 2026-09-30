using NUnit.Framework;

namespace Aero.UnitTests
{
    public class DiagnosticTests
    {
        private static GeneratorHarness.Result Run(string fields) => GeneratorHarness.Run(@"
using Aero.Gen.Attributes;

namespace DiagnosticTest
{
    [AeroBlock]
    public struct Block
    {
        public uint Value;
    }

    [Aero]
    public partial class Test
    {
" + fields + @"
    }
}");

        [TestCase("[AeroArray(typeof(byte))] public uint Value;")]
        [TestCase("[AeroArray(4)] public Block Value;")]
        [TestCase("[AeroArray(typeof(byte))] [AeroString(typeof(byte))] public string Value;")]
        [TestCase("[AeroString(typeof(byte))] public uint Value;")]
        [TestCase("[AeroString] public byte Value;")]
        [TestCase("[AeroArray(2)] [AeroString(4)] public uint[] Value;")]
        public void AttributeOnWrongType(string field)
        {
            Assert.That(Run(field).GeneratorDiagnosticIds, Does.Contain("Aero19"));
        }

        [TestCase("[AeroArray(typeof(byte))] public uint[] Value;")]
        [TestCase("[AeroString(typeof(byte))] public string Value;")]
        [TestCase("[AeroArray(2)] [AeroString(4)] public string[] Value;")]
        public void AttributeOnRightType(string field)
        {
            var result = Run(field);
            Assert.That(result.GeneratorDiagnosticIds, Does.Not.Contain("Aero19"));
            Assert.That(result.CompileErrors, Is.Empty);
        }

        [TestCase("[AeroArray(typeof(ushort), Chunked = true)] public uint[] Value;")]
        [TestCase("[AeroArray(4, Chunked = true)] public uint[] Value;")]
        [TestCase("[AeroArray(4, LengthIsBytes = true)] public uint[] Value;")]
        [TestCase("[AeroArray(typeof(byte), Chunked = true, LengthIsBytes = true)] public uint[] Value;")]
        public void InvalidArrayArguments(string field)
        {
            Assert.That(Run(field).GeneratorDiagnosticIds, Does.Contain("Aero16"));
        }
    }
}
