using System.Numerics;
using Aero.Gen.Attributes;

namespace Aero.TestCases
{
    [AeroBlock]
    public struct Melding_ObserverView_ShadowFieldUpdate
    {
        [AeroString]
        public string? PerimiterSetName;
        public ActiveDataStruct? ActiveData;
        public ScopeBubbleInfoData? ScopeBubbleInfo;
    }
    
    [AeroBlock]
    public struct ActiveDataStruct
    {
        [AeroArray(4)]
        public byte[] Unk1;

        [AeroArray(13)]
        public byte[] Unk2_Consistent;

        [AeroArray(typeof(byte))]
        public Vector3[] ControlPoints_1;

        [AeroArray(typeof(byte))]
        public Vector3[] Offsets_1;

        [AeroArray(typeof(byte))]
        public Vector3[] ControlPoints_2;

        [AeroArray(typeof(byte))]
        public Vector3[] Offsets_2;
    }
    
    [AeroBlock]
    public struct ScopeBubbleInfoData
    {
        // Don't know how this works but its used everywhere so keeping it in a struct
        [AeroArray(8)]
        public byte[] Unk;
    }
}