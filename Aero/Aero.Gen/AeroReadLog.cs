using System;

namespace Aero.Gen
{
    public class AeroReadLog
    {
        public string       ParentName;
        public string       Name;
        public int          Offset;
        public int          Length;
        public string       TypeStr;
        public Type         Type;
        public LogEntryType EntryType;

        public AeroReadLog()
        {
            
        }
        
        public AeroReadLog(string parentName, string name, int offset, int length, string typeStr, Type type)
        {
            ParentName = parentName != "" ? parentName : null;
            Name       = name;
            Offset     = offset;
            Length     = length;
            TypeStr    = typeStr;
            Type       = type;
            EntryType = LogEntryType.Field;
        }
        
        public AeroReadLog(string parentName, string name, bool isArray, Type type, int offset)
        {
            ParentName = parentName != "" ? parentName : null;
            Name       = name;
            Offset     = offset;
            Length     = 0;
            Type       = type;
            EntryType  = isArray ? LogEntryType.Array : LogEntryType.AeroBlock;
        }

        public override string ToString()
        {
            var str = $"{ParentName}, {Name}, {Offset}, {Length}, {TypeStr}, ({Type}), {EntryType}";

            return str;
        }

        public enum LogEntryType
        {
            Field,
            Array,
            AeroBlock
        }
    }
}