using System.Linq;
using System.Text;
using Aero.Gen;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    public enum DisplayOrder : ushort
    {
        kill,
        defend,
        use,
        killncollect,
        meet,
        @default
    }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter("enumtest")]
    public partial class EncounterEnumView
    {
        private short progress;

        private DisplayOrder display_order;
    }

    public class EncounterEnumTests
    {
        private static byte[] Str(string str) => Encoding.UTF8.GetBytes(str);

        [Test]
        public void HeaderListsTheValueNames()
        {
            // The display_order part is the header of an arc schema in the 1962 NSRs
            var expected = Str("enumtest\0")
                          .Concat(new byte[] {0x00, 0x06, 0x01}).Concat(Str("progress\0"))
                          .Concat(new byte[] {0x01, 0x04, 0x01}).Concat(Str("display_order\0"))
                          .Concat(Str("\u0001kill\u0001defend\u0001use\u0001killncollect\u0001meet\u0001default\u0001\0"))
                          .ToArray();

            Assert.That(new EncounterEnumView().GetHeader(), Is.EqualTo(expected));
        }

        [Test]
        public void ValueIsTwoBytes()
        {
            var view = new EncounterEnumView {display_orderProp = DisplayOrder.killncollect};
            view.ClearViewChanges();
            view.display_orderProp = DisplayOrder.meet;

            var buffer = new byte[view.GetPackedChangesSize()];
            Assert.That(view.PackChanges(buffer), Is.EqualTo(3));
            Assert.That(buffer, Is.EqualTo(new byte[] {0x01, 0x04, 0x00}));

            var read = new EncounterEnumView();
            Assert.That(read.UnpackChanges(new byte[] {0x01, 0x05, 0x00}), Is.EqualTo(3));
            Assert.That(read.display_orderProp, Is.EqualTo(DisplayOrder.@default));
        }

        [TestCase("byte")]
        [TestCase("ByteEnum")]
        public void ByteFieldsAreRejected(string type)
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen;
using Aero.Gen.Attributes;

namespace EncounterTest
{
    public enum ByteEnum : byte { A, B }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter(""test"")]
    public partial class TestView
    {
        private " + type + @" value;
    }
}");

            Assert.That(result.GeneratorDiagnosticIds, Does.Contain("Aero7"));
        }

        [TestCase("A = 0, B = 2")]
        [TestCase("")]
        public void EnumsWithGapsAreRejected(string values)
        {
            var result = GeneratorHarness.Run(@"
using Aero.Gen;
using Aero.Gen.Attributes;

namespace EncounterTest
{
    public enum GapEnum : ushort { " + values + @" }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter(""test"")]
    public partial class TestView
    {
        private GapEnum value;
    }
}");

            Assert.That(result.GeneratorDiagnosticIds, Does.Contain("Aero17"));
        }
    }
}
