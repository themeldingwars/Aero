using System;

namespace Aero.Gen.Attributes
{    
    [AttributeUsage(AttributeTargets.Class)]
    public class AeroAttribute : Attribute
    {
        public static string Name = "Aero";

        public bool IsView = false;

        public AeroAttribute(bool isView = false)
        {
            IsView = isView;
        }

    }
}