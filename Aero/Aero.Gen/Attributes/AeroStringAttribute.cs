using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroStringAttribute : Attribute
    {
        public const string Name = "AeroString";
        
        public AeroStringAttribute()
        {
            
        }
        
        public AeroStringAttribute(int length)
        {
            
        }
        
        public AeroStringAttribute(string length)
        {
            
        }
        
        public AeroStringAttribute(Type lengthType)
        {
            
        }
    }
}