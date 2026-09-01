using System;

namespace Aero.Gen.Attributes
{
    // Used to mark a byte[] field as a blob of raw bytes, the contents are read as a whole
    // and parsed elsewhere
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroBlobAttribute : Attribute
    {
        public static string Name = "AeroBlob";

        public string Key;
        public Type   Typ;

        public AeroBlobAttribute()
        {
        }

        public AeroBlobAttribute(string key)
        {
            Key = key;
        }

        public AeroBlobAttribute(Type typ)
        {
            Typ = typ;
        }
    }
}
