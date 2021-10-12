using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroStringAttribute : Attribute
    {
        public const string Name = "AeroString";

        public int    Length;
        public string LengthStr;
        public Type LengthType;
        
        public AeroStringAttribute()
        {
            
        }
        
        public AeroStringAttribute(int length)
        {
            Length = length;
        }
        
        public AeroStringAttribute(string length)
        {
            LengthStr = length;
        }
        
        public AeroStringAttribute(Type lengthType)
        {
            LengthType = lengthType;
        }
    }
}