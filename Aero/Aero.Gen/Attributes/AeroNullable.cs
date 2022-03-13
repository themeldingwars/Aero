using System;

namespace Aero.Gen.Attributes
{
    // Mark a field as nullable, only for use in views
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroNullable : Attribute
    {
        public static string Name = "AeroNullable";
    }
}