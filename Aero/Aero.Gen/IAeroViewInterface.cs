using System;

namespace Aero.Gen
{
    public interface IAeroViewInterface : IAero
    {
        // Unpacks a view update to this class, returns how many bytes were read
        public int UnpackChanges(ReadOnlySpan<byte> data);

        // Gets the number of bytes needed to pack all the changes.
        public int GetPackedChangesSize();
        
        // Packs what has changed into the buffer and returns the number of bytes written
        // ClearViewChanges should be called after you pack the if clearDirtyAfterSend wasn't set, this reset the change field tracking
        public int PackChanges(Span<byte> buffer, bool clearDirtyAfterSend = true);

        // Will clear the internal data tracking if a field has been changed
        public void ClearViewChanges();
        
        // Returns the name for the shadow field id
        public string ShadowFieldIdToName(int id);
        
        // Returns a type for the shadow field id
        public Type ShadowFieldIdToType(int id);
        
        // Get a list of the shadow fields in this view, with data if they are nullable and their id
        public (string, int, Type, bool)[] GetShadowFieldsData();
    }
}