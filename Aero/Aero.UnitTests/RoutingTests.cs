using System;
using System.Linq;
using Aero.Gen;
using Aero.Gen.Attributes;
using Aero.Protocol;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using static Aero.Gen.Attributes.AeroMessageIdAttribute;

namespace Aero.UnitTests
{
    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Command, MatrixMessage.SuperPing, MatrixVersion.V7)]
    public partial class MatrixCommandDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.SuperPing, MatrixVersion.V7)]
    public partial class MatrixMessageDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.SuperPong, MatrixVersion.V7)]
    public partial class MatrixMessagePongDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Command, MatrixMessage.EnterZoneAck)]
    public partial class MatrixV1CommandDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.EnterZoneAck)]
    public partial class MatrixV1MessageDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.EnterZone)]
    public partial class MatrixV1MessageEnterZoneDispatchTestMessage
    {
    }

    [Aero(AeroGenTypes.View)]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterView.BaseController)]
    public partial class GssViewDispatchTestController
    {
    }

    [Aero(AeroGenTypes.View)]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterView.ObserverView, GssVersion.V1, GssVersion.V30)]
    public partial class GssViewDispatchTestViewEarly
    {
    }

    [Aero(AeroGenTypes.View)]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterView.ObserverView, GssVersion.V31)]
    public partial class GssViewDispatchTestViewLate
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterMessage.Killed, GssCharacterView.CombatView)]
    public partial class GssViewRoutedKilledViaCombatView
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterMessage.CharacterLoaded, GssCharacterView.ObserverView, GssVersion.V11, GssVersion.V30)]
    public partial class GssViewRoutedLoadedEarly
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterMessage.CharacterLoaded, GssCharacterView.ObserverView, GssVersion.V31)]
    public partial class GssViewRoutedLoadedLate
    {
    }

    [Aero]
    [AeroMessageId(MsgType.GSS, MsgSrc.Command, GssCharacterCommand.ActivateAbility, GssCharacterView.BaseController)]
    public partial class GssViewRoutedActivateAbilityViaBaseController
    {
    }

    public class RoutingTests
    {
        [Test]
        public void MatrixTables_RoundTrip()
        {
            const MatrixVersion version = MatrixVersion.V7;
            const MatrixMessage message = MatrixMessage.Announce;

            byte id = MatrixTables.GetMessageId(version, message);
            ClassicAssert.Greater(id, (byte)0, $"MatrixMessage.{message} should exist in {version}");
            ClassicAssert.AreEqual((int)message, MatrixTables.FindMessage(version, id));
            ClassicAssert.AreEqual(MatrixMessage.Announce, AeroRouting.GetMatrixMessage(version, id).Value);
            ClassicAssert.Less(MatrixTables.FindMessage(version, 0), 0);
        }

        [Test]
        public void MatrixTables_TryGetMessageId_MatchesGetMessageId()
        {
            const MatrixVersion version = MatrixVersion.V7;
            const MatrixMessage message = MatrixMessage.Announce;

            ClassicAssert.IsTrue(MatrixTables.TryGetMessageId(version, message, out byte id));
            ClassicAssert.AreEqual(MatrixTables.GetMessageId(version, message), id);
        }

        [Test]
        public void MatrixTables_TryGetMessageId_Absent_ReturnsFalse()
        {
            ClassicAssert.IsFalse(MatrixTables.TryGetMessageId(MatrixVersion.V7, MatrixMessage.UpdateDevZoneInfo, out byte id));
            ClassicAssert.AreEqual(0, id);
        }

        [Test]
        public void MatrixRouting_MsgSrc_SelectsRegisteredHandler()
        {
            const MatrixVersion version = MatrixVersion.V7;

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Command, MatrixMessage.SuperPing) is MatrixCommandDispatchTestMessage);
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Message, MatrixMessage.SuperPing) is MatrixMessageDispatchTestMessage);
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Message, MatrixMessage.SuperPong) is MatrixMessagePongDispatchTestMessage);
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(version, MsgSrc.Command, MatrixMessage.SuperPong));
        }

        [Test]
        public void MatrixRouting_WireIds_RespectMsgSrc()
        {
            const MatrixVersion version = MatrixVersion.V1;
            byte ezaId = MatrixTables.GetMessageId(version, MatrixMessage.EnterZoneAck);
            byte ezId = MatrixTables.GetMessageId(version, MatrixMessage.EnterZone);

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Command, ezaId) is MatrixV1CommandDispatchTestMessage);
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Message, ezaId) is MatrixV1MessageDispatchTestMessage);
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Message, ezId) is MatrixV1MessageEnterZoneDispatchTestMessage);
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Command, ezId));
        }

        [Test]
        public void GssRoot_RoundTrip()
        {
            var ordinal = (int)GssMessage.RequestLogout;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Root, GssTables.Kind.Message, ordinal);

            ClassicAssert.AreEqual(GssTables.Ns.Root, GssTables.FindNamespace(version, typecode, GssTables.Kind.Message));
            ClassicAssert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Message, id));
            ClassicAssert.AreEqual(ordinal, AeroRouting.GetGssMessageOrdinal(version, typecode, GssTables.Kind.Message, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Root, GssTables.Kind.Message, ordinal);
            ClassicAssert.AreEqual(typecode, typecode2);
            ClassicAssert.AreEqual(id, id2);
        }

        [Test]
        public void GssCharacterMessage_RoundTrip()
        {
            var ordinal = (int)GssCharacterMessage.CharacterLoaded;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Message, ordinal);

            ClassicAssert.AreEqual(GssTables.Ns.Character, GssTables.FindNamespace(version, typecode, GssTables.Kind.Message));
            ClassicAssert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Message, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, ordinal);
            ClassicAssert.AreEqual(typecode, typecode2);
            ClassicAssert.AreEqual(id, id2);
        }

        [Test]
        public void GssCharacterCommand_RoundTrip()
        {
            var ordinal = (int)GssCharacterCommand.ActivateAbility;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Command, ordinal);

            ClassicAssert.AreEqual(GssTables.Ns.Character, GssTables.FindNamespace(version, typecode, GssTables.Kind.Command));
            ClassicAssert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Command, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Character, GssTables.Kind.Command, ordinal);
            ClassicAssert.AreEqual(typecode, typecode2);
            ClassicAssert.AreEqual(id, id2);
        }

        [Test]
        public void GssTables_TryGetWireIds_Message()
        {
            var ordinal = (int)GssCharacterMessage.CharacterLoaded;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Message, ordinal);

            ClassicAssert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterMessage.CharacterLoaded, out byte wireTypecode, out byte wireMessageId));
            ClassicAssert.AreEqual(typecode, wireTypecode);
            ClassicAssert.AreEqual(id, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_Command()
        {
            var ordinal = (int)GssCharacterCommand.ActivateAbility;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Command, ordinal);

            ClassicAssert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterCommand.ActivateAbility, out byte wireTypecode, out byte wireMessageId));
            ClassicAssert.AreEqual(typecode, wireTypecode);
            ClassicAssert.AreEqual(id, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_View()
        {
            var ordinal = (int)GssCharacterView.ObserverView;
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, ordinal);

            ClassicAssert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterView.ObserverView, out byte wireTypecode, out byte wireMessageId));
            ClassicAssert.AreEqual(typecode, wireTypecode);
            ClassicAssert.AreEqual(0, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_UnknownEnum_ReturnsFalse()
        {
            ClassicAssert.IsFalse(GssTables.TryGetWireIds(GssVersion.V1, 123, out byte typecode, out byte messageId));
            ClassicAssert.AreEqual(0, typecode);
            ClassicAssert.AreEqual(0, messageId);
        }

        [Test]
        public void GssTables_UnknownTypecode_IsUnknown()
        {
            ClassicAssert.AreEqual(GssTables.Ns.Unknown, GssTables.FindNamespace(GssVersion.V1, 255, GssTables.Kind.Message));
            ClassicAssert.AreEqual(GssTables.Ns.Unknown, GssTables.FindNamespace(GssVersion.V1, 255, GssTables.Kind.Command));
            ClassicAssert.Less(GssTables.FindMessage(GssVersion.V1, 255, GssTables.Kind.Message, 1), 0);
        }

        [Test]
        public void GssTables_TryFindView_RoundTrip()
        {
            var ordinal = (int)GssCharacterView.ObserverView;
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, ordinal);

            ClassicAssert.AreEqual(ordinal, GssTables.FindView(version, typecode));
            ClassicAssert.IsTrue(GssTables.TryFindView(version, typecode, out int ns, out int foundOrdinal));
            ClassicAssert.AreEqual(GssTables.Ns.Character, ns);
            ClassicAssert.AreEqual(ordinal, foundOrdinal);

            ClassicAssert.IsFalse(GssTables.TryFindView(version, 255, out int unknownNs, out int unknownOrdinal));
            ClassicAssert.AreEqual(GssTables.Ns.Unknown, unknownNs);
            ClassicAssert.AreEqual(-1, unknownOrdinal);
        }

        [Test]
        public void GssViewRouting_WireTypecode_ReturnsRegisteredView()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode) is GssViewDispatchTestController);
        }

        [Test]
        public void GssViewRouting_MessageId_IsIgnoredForSelection()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode) is GssViewDispatchTestController);
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 42, typecode) is GssViewDispatchTestController);
        }

        [Test]
        public void GssViewRouting_VersionRange_SelectsCorrectClass()
        {
            var earlyTypecode = GssTables.GetMessageId(GssVersion.V10, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var lateTypecode = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            ClassicAssert.Greater(earlyTypecode, (byte)0);
            ClassicAssert.Greater(lateTypecode, (byte)0);

            AeroRouting.CurrentGssVersion = GssVersion.V10;
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 0, earlyTypecode) is GssViewDispatchTestViewEarly);

            AeroRouting.CurrentGssVersion = GssVersion.V40;
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 0, lateTypecode) is GssViewDispatchTestViewLate);
        }

        [Test]
        public void GssViewRouting_CommandSrc_ReturnsNull()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Command, 1, typecode));
        }

        [Test]
        public void GssViewRouting_UnknownTypecode_ReturnsNull()
        {
            AeroRouting.CurrentGssVersion = GssVersion.V1;
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, 255));
        }

        [Test]
        public void GssViewRouting_UnregisteredView_ReturnsNull()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            AeroRouting.CurrentGssVersion = version;

            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode));
        }

        [Test]
        public void GssViewRouting_DirectEnumOverload()
        {
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterView.BaseController) is GssViewDispatchTestController);
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterView.BaseController));
        }

        [Test]
        public void GssViewRoutedMessage_TryGetWireIds_ReturnsViewRouteAndMessageId()
        {
            var (version, viewTypecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            var messageId = GssTables.GetMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);
            ClassicAssert.Greater(messageId, (byte)0);

            ClassicAssert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterView.CombatView, GssCharacterMessage.Killed, out byte wireTypecode, out byte wireMessageId));
            ClassicAssert.AreEqual(viewTypecode, wireTypecode);
            ClassicAssert.AreEqual(messageId, wireMessageId);

            // The view-less lookup returns the namespace route's typecode for the same message.
            ClassicAssert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterMessage.Killed, out byte nsTypecode, out byte nsMessageId));
            ClassicAssert.AreEqual(messageId, nsMessageId);
            ClassicAssert.AreNotEqual(viewTypecode, nsTypecode);
        }

        [Test]
        public void GssViewRoutedMessage_WireDispatch_ReturnsRegisteredClass()
        {
            var (version, viewTypecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            var messageId = GssTables.GetMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, messageId, viewTypecode) is GssViewRoutedKilledViaCombatView);
        }

        [Test]
        public void GssViewRoutedMessage_NamespaceRoute_DoesNotMatch()
        {
            // Killed is registered via the CombatView route, so the plain Character namespace route must not dispatch it.
            var nsTypecode = GssTables.GetNamespaceTypecode(GssVersion.V1, GssTables.Ns.Character);
            var messageId = GssTables.GetMessageId(GssVersion.V1, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);
            ClassicAssert.Greater(nsTypecode, (byte)0);
            ClassicAssert.Greater(messageId, (byte)0);

            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, messageId, nsTypecode));
        }

        [Test]
        public void GssViewRoutedMessage_VersionRange_SelectsCorrectClass()
        {
            var earlyTypecode = GssTables.GetMessageId(GssVersion.V20, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var lateTypecode = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var earlyId = GssTables.GetMessageId(GssVersion.V20, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.CharacterLoaded);
            var lateId = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.CharacterLoaded);
            ClassicAssert.Greater(earlyTypecode, (byte)0);
            ClassicAssert.Greater(lateTypecode, (byte)0);
            ClassicAssert.Greater(earlyId, (byte)0);
            ClassicAssert.Greater(lateId, (byte)0);

            try
            {
                AeroRouting.CurrentGssVersion = GssVersion.V20;
                ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, earlyId, earlyTypecode) is GssViewRoutedLoadedEarly);

                AeroRouting.CurrentGssVersion = GssVersion.V40;
                ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, lateId, lateTypecode) is GssViewRoutedLoadedLate);
            }
            finally
            {
                AeroRouting.CurrentGssVersion = GssVersion.V1;
            }
        }

        [Test]
        public void GssViewRoutedCommand_WireDispatch_ReturnsRegisteredClass()
        {
            var (version, viewTypecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            var commandId = GssTables.GetMessageId(version, GssTables.Ns.Character, GssTables.Kind.Command, (int)GssCharacterCommand.ActivateAbility);
            ClassicAssert.Greater(commandId, (byte)0);

            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Command, commandId, viewTypecode) is GssViewRoutedActivateAbilityViaBaseController);
        }

        [Test]
        public void GssViewRoutedMessage_DirectOverload()
        {
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterMessage.Killed, GssCharacterView.CombatView) is GssViewRoutedKilledViaCombatView);
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterMessage.Killed));
        }

        [Test]
        public void GssViewRoutedCommand_DirectOverload()
        {
            ClassicAssert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterCommand.ActivateAbility, GssCharacterView.BaseController) is GssViewRoutedActivateAbilityViaBaseController);
            ClassicAssert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterCommand.ActivateAbility));
        }

        static (GssVersion Version, byte Typecode, byte Id) FindAvailable(int nsIndex, int kind, int ordinal)
        {
            foreach (var value in Enum.GetValues(typeof(GssVersion)))
            {
                var version = (GssVersion)value;
                var typecode = GssTables.GetNamespaceTypecode(version, nsIndex);
                if (typecode == 255)
                    continue;

                var id = GssTables.GetMessageId(version, nsIndex, kind, ordinal);
                if (id != 0)
                    return (version, typecode, id);
            }

            Assert.Fail($"No protocol version found with ns={nsIndex} kind={kind} ordinal={ordinal}");
            return default;
        }

        static (GssVersion Version, byte Typecode) FindAvailableView(int nsIndex, int viewOrdinal)
        {
            foreach (var value in Enum.GetValues(typeof(GssVersion)))
            {
                var version = (GssVersion)value;
                var typecode = GssTables.GetMessageId(version, nsIndex, GssTables.Kind.View, viewOrdinal);
                if (typecode != 0)
                    return (version, typecode);
            }

            Assert.Fail($"No protocol version found with view ns={nsIndex} ordinal={viewOrdinal}");
            return default;
        }
    }
}
