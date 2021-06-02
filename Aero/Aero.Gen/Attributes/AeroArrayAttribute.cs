using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroArrayAttribute : Attribute
    {
        public static string Name = "AeroArray";

        public AeroArrayAttribute()
        {
        }
        
        public AeroArrayAttribute(int length)
        {
        }
        
        public AeroArrayAttribute(string key)
        {
        }
        
        public AeroArrayAttribute(Type typ)
        {
        }
    }
}