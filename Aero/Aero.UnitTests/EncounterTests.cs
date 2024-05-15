using System;
using System.Linq;
using Aero.Gen;
using Aero.Gen.Attributes;
using NUnit.Framework;

namespace Aero.UnitTests
{
    [Aero(AeroGenTypes.View)]
    [AeroEncounter("arc")]
    public partial class ArcView
    {
        private uint arc_name;

        private uint activity_string;

        private ushort activity_visible;

        [AeroNullable]
        private EntityId healthbar_1;

        private ushort healthbar_1_visible;
    }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter("AirTrafficControl")]
    public partial class AirTrafficControlView
    {
        // has no fields
    }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter("MoreTypes")]
    public partial class MoreTypesView
    {
        private uint name;

        [AeroArray(2)]
        private uint[] localizedTexts;

        private ulong eta;

        [AeroNullable]
        private EntityId healthbar_1;

        [AeroArray(3)]
        private bool[] booleans;
    }

    [Aero(AeroGenTypes.View)]
    [AeroEncounter("default")]
    public partial class HudTimerView
    {
        private Timer hudtimer_timer;

        private uint hudtimer_label;
    }

    [AeroBlock]
    public struct EntityId
    {
        public ulong Backing;
    }

    [Flags]
    public enum TimerState : byte
    {
        CountingDown = 1,
        Paused = 2,
    }

    [AeroBlock]
    public struct Timer
    {
        public TimerState State;

        [AeroIf(nameof(State), AeroIfAttribute.Ops.DoesntHaveFlag, TimerState.Paused)]
        public ulong Micro;

        [AeroIf(nameof(State), AeroIfAttribute.Ops.HasFlag, TimerState.Paused)]
        public float Seconds;
    }

    public class EncounterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ArcHeaderTest()
        {
            var arc = new ArcView();

            var expectedHeader = new byte[]
            {
                0x61, 0x72, 0x63, 0x00,
                0x00, 0x00, 0x01,
                0x61, 0x72, 0x63, 0x5F, 0x6E, 0x61, 0x6D, 0x65, 0x00,
                0x01, 0x00, 0x01,
                0x61, 0x63, 0x74, 0x69, 0x76, 0x69, 0x74, 0x79, 0x5F, 0x73, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x00,
                0x02, 0x06, 0x01,
                0x61, 0x63, 0x74, 0x69, 0x76, 0x69, 0x74, 0x79, 0x5F, 0x76, 0x69, 0x73, 0x69, 0x62, 0x6C, 0x65, 0x00,
                0x03, 0x02, 0x01,
                0x68, 0x65, 0x61, 0x6C, 0x74, 0x68, 0x62, 0x61, 0x72, 0x5F, 0x31, 0x00,
                0x04, 0x06, 0x01,
                0x68, 0x65, 0x61, 0x6C, 0x74, 0x68, 0x62, 0x61, 0x72, 0x5F, 0x31, 0x5F, 0x76, 0x69, 0x73, 0x69, 0x62, 0x6C, 0x65, 0x00,
            };

            if (expectedHeader.SequenceEqual(arc.GetHeader()))
            {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void AirTrafficControlHeaderTest()
        {
            var atc = new AirTrafficControlView();

            var expectedHeader = new byte[]
            {
                0x41, 0x69, 0x72, 0x54, 0x72, 0x61, 0x66, 0x66, 0x69, 0x63, 0x43, 0x6F, 0x6E, 0x74, 0x72, 0x6F, 0x6C, 0x00,
            };

            if (expectedHeader.SequenceEqual(atc.GetHeader()))
            {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void HudTimerPackChangesTest()
        {
            var hudTimer = new HudTimerView
            {
                hudtimer_timerProp = new Timer() {State = TimerState.Paused, Seconds = 15},
                hudtimer_labelProp = 10082,
            };

            var bytes = new byte[]
            {
                0x00,
                0x02, 0x00, 0x00, 0x70, 0x41,
                0x01,
                0x62, 0x27, 0x00, 0x00
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked = hudTimer.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void MoreTypesPackChangesTest()
        {
            var encounter = new MoreTypesView
            {
                nameProp = 10093,
                booleansProp = new bool[] { true, false, true },
            };

            var bytes = new byte[]
            {
                0x00,
                0x6D, 0x27, 0x00, 0x00,

                0x04, 0x00,
                0x01,
                0x04, 0x01,
                0x00,
                0x04, 0x02,
                0x01,
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked = encounter.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void MoreTypesPackChangesWithNullableNullTest()
        {
            var encounter = new MoreTypesView
            {
                nameProp = 10329,
                healthbar_1Prop = null,
            };

            var bytes = new byte[]
            {
                0x00,
                0x59, 0x28, 0x00, 0x00,

                0x83,
            };
            var packedBuffer = new byte[bytes.Length];
            var lenPacked = encounter.PackChanges(packedBuffer);

            if (lenPacked == packedBuffer.Length && bytes.SequenceEqual(packedBuffer)) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void HudTimerUnpackChangesTest()
        {
            var hudTimer = new HudTimerView
            {
                hudtimer_timerProp = new Timer() {State = TimerState.Paused, Seconds = 15},
                hudtimer_labelProp = 10329,
            };

            var bytes = new byte[]
            {
                0x00,
                0x01, 0x40, 0x79, 0x39, 0xB3, 0x81, 0x18, 0x06, 0x00,
                0x01,
                0x62, 0x27, 0x00, 0x00
            };
            var lenUnpacked = hudTimer.UnpackChanges(bytes);

            if (lenUnpacked == bytes.Length
                && hudTimer.hudtimer_labelProp == 10082
                && hudTimer.hudtimer_timerProp.State == TimerState.CountingDown
                && hudTimer.hudtimer_timerProp.Micro == 1715795197000000) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void MoreTypesUnpackChangesTest()
        {
            var encounter = new MoreTypesView
            {
                nameProp = 10082,
                booleansProp = new bool[] { true, false, true },
            };

            var bytes = new byte[]
            {
                0x00,
                0x6D, 0x27, 0x00, 0x00,

                0x04, 0x00,
                0x00,
                0x04, 0x01,
                0x01,
                0x04, 0x02,
                0x00,
            };
            var lenUnpacked = encounter.UnpackChanges(bytes);

            if (lenUnpacked == bytes.Length
                && encounter.nameProp == 10093
                && encounter.booleansProp.SequenceEqual(new bool[] { false, true, false })
                ) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void MoreTypesUnpackChangesWithNullableNullTest()
        {
            var encounter = new MoreTypesView
            {
                localizedTextsProp = new uint[] { 10342, 10343 },
                healthbar_1Prop = new EntityId() { Backing = 2305005520655996928 },
            };

            var bytes = new byte[]
            {
                0x01, 0x00,
                0x69, 0x28, 0x00, 0x00,
                0x01, 0x01,
                0x6A, 0x28, 0x00, 0x00,

                0x83,
            };
            var lenUnpacked = encounter.UnpackChanges(bytes);

            if (lenUnpacked == bytes.Length
                && encounter.localizedTextsProp.SequenceEqual(new uint[] { 10345, 10346})
                && encounter.healthbar_1Prop == null) {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public void MoreTypesGetPackedChangesSizeTest()
        {
            var encounter = new MoreTypesView
            {
                nameProp = 10093,
                localizedTextsProp = new uint[] { 10142, 10227 },
                etaProp = 1715795197000000,
                healthbar_1Prop = new EntityId() { Backing = 2305005520655996928 },
                booleansProp = new bool[] { false, false, true },
            };

            var lenPacked = encounter.GetPackedChangesSize();

            if (lenPacked == 44) {
                Assert.Pass();
            }

            Assert.Fail();
        }
    }
}