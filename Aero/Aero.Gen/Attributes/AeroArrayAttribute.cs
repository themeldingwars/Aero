using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroArrayAttribute : Attribute
    {
        public static string Name = "AeroArray";

        public int    Length;
        public string Key;
        public Type   Typ;

        // Only with typeof(byte): the array is a sequence of chunks, each with its own count byte.
        // A count of 255 means another chunk follows, so 255 elements are written as FF ... 00
        public bool Chunked;

        public AeroArrayAttribute()
        {
        }

        public AeroArrayAttribute(int length)
        {
            Length = length;
        }

        public AeroArrayAttribute(string key)
        {
            Key = key;
        }

        public AeroArrayAttribute(Type typ)
        {
            Typ = typ;
        }
    }
}