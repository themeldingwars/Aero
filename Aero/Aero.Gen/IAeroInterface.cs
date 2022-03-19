using System;

namespace Aero.Gen
{
    public interface IAero
    {
        // Unpacks the data from the span buffer into the instance of this object.
        // If this is a view then Unpack will unpack the keyframe message
        public int Unpack(ReadOnlySpan<byte> data);
        
        // Get how many bytes are needed to pack this object
        public int GetPackedSize();

        // Packs all the data in this class into the buffer, returns how many bytes were packed
        public int Pack(Span<byte> buffer);
    }
}