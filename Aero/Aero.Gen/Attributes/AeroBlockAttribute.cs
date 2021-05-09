using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class AeroBlockAttribute : Attribute
    {
        public static string Name = "AeroBlock";

    }
}