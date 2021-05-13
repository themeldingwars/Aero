using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class AeroIfAttribute : Attribute
    {
        public static string Name = "AeroIf";

        public enum Ops
        {
            Equal,
            NotEqual,
            HasFlag,
            DoesntHaveFlag
        }

        public AeroIfAttribute(string key, int value, Ops op = Ops.Equal)
        {
            
        }
        
        public AeroIfAttribute(string key, string value, Ops op = Ops.Equal)
        {
            
        }
        
        public AeroIfAttribute(string key, Ops  op, params object[] values)
        {
            
        }
        
        public AeroIfAttribute(string key, object value, Ops op = Ops.Equal)
        {
            
        }
    }
}