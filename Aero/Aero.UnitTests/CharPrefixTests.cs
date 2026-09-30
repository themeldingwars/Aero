using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero]
    public partial class CharPrefixTest
    {
        [AeroString(typeof(char))] public string Name;
        public char                              Letter;
    }

    public class CharPrefixTests
    {
        [Test]
        public void RoundTrip()
        {
            var msg    = new CharPrefixTest {Name = "Aero", Letter = 'x'};
            var buffer = new byte[msg.GetPackedSize()];

            Assert.That(msg.Pack(buffer), Is.EqualTo(1 + 4 + 1));
            Assert.That(buffer, Is.EqualTo(new byte[] {4, (byte)'A', (byte)'e', (byte)'r', (byte)'o', (byte)'x'}));

            var read = new CharPrefixTest();
            Assert.That(read.Unpack(buffer), Is.EqualTo(buffer.Length));
            Assert.That(read.Name, Is.EqualTo("Aero"));
            Assert.That(read.Letter, Is.EqualTo('x'));
        }
    }
}
