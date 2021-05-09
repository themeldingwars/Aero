using System;

namespace Aero.Gen.Attributes
{    
    [AttributeUsage(AttributeTargets.Class)]
    public class AeroAttribute : Attribute
    {
        public static string Name = "Aero";

    }
}