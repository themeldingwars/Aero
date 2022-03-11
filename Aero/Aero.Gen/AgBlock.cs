using System;

namespace Aero.Gen
{
    public class AgBlock : IDisposable
    {
        private Genv2  Gen;
        private Func<bool> Close;
        private bool   DefaultAddNoContent;

        public AgBlock(Genv2 gen, Action open, Func<bool> close = null, bool noOpenBracket = false, bool defaultAddNoContent = false)
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
            var endBracket = Close?.Invoke() ?? !DefaultAddNoContent;
            if (endBracket is true) {
                Gen.UnIndent();
                Gen.AddLine("}");
            }
        }
    }
}