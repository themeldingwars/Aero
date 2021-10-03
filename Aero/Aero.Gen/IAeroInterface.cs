using System;

namespace Aero.Gen
{
    public interface IAero
    {
        public int Unpack(ReadOnlySpan<byte> data);

        public int GetPackedSize();

        public int Pack(Span<byte> buffer);
    }
}