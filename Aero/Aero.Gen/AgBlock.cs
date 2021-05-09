using System;

namespace Aero.Gen
{
    public class AgBlock : IDisposable
    {
        private Genv2  Gen;
        private Action Close;
        private bool   DefaultAddNoContent;

        public AgBlock(Genv2 gen, Action open, Action close = null, bool noOpenBracket = false, bool defaultAddNoContent = false)
        {
            Gen                 = gen;
            Close               = close;
            DefaultAddNoContent = defaultAddNoContent;

            open();
            if (!DefaultAddNoContent) {
                if (!noOpenBracket) Gen.AddLine("{");
                Gen.Indent();
            }
        }

        public void Dispose()
        {
            Close?.Invoke();
            if (!DefaultAddNoContent) {
                Gen.UnIndent();
                Gen.AddLine("}");
            }
        }
    }
}