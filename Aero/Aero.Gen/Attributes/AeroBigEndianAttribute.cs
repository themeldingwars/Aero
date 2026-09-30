using System;

namespace Aero.Gen.Attributes
{
    // Reads and writes an integer field (or an array of them) in network byte order
    [AttributeUsage(AttributeTargets.Field)]
    public class AeroBigEndianAttribute : Attribute
    {
        public static string Name = "AeroBigEndian";
    }
}