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