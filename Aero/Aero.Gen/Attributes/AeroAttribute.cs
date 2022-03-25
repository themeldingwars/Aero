using System;

namespace Aero.Gen.Attributes
{    
    [AttributeUsage(AttributeTargets.Class)]
    public class AeroAttribute : Attribute
    {
        public static string Name = "Aero";

        public AeroGenTypes AeroType = AeroGenTypes.Normal;

        public AeroAttribute()
        {
            AeroType = AeroGenTypes.Normal;
        }
        
        public AeroAttribute(AeroGenTypes type)
        {
            AeroType = type;
        }

    }
}