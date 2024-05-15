using System;

namespace Aero.Gen.Attributes
{    
    [AttributeUsage(AttributeTargets.Class)]
    public class AeroEncounterAttribute : Attribute
    {
        public static string Name = "AeroEncounter";

        public string EncounterType;

        public AeroEncounterAttribute(string encounterType)
        {
            EncounterType = encounterType;
        }
    }
}
