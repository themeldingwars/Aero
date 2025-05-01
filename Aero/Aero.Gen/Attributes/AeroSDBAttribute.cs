using System;

namespace Aero.Gen.Attributes
{
    // Used to mark a field as containing a value in the SDB
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
    public class AeroSdbAttribute : Attribute
    {
        public static string Name = "AeroSdb";
        
        public        string tableName;
        public        string columnName;

        public AeroSdbAttribute(string tableName, string columnName)
        {
            this.tableName  = tableName;
            this.columnName = columnName;
        }
    }
}