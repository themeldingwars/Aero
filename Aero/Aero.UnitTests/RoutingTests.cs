using System;
using System.Linq;
using Aero.Gen;
using Aero.Gen.Attributes;
using Aero.Protocol;
using NUnit.Framework;
using static Aero.Gen.Attributes.AeroMessageIdAttribute;

namespace Aero.UnitTests
{
    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Command, MatrixMessage.SuperPing)]
    public partial class MatrixCommandDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.SuperPing)]
    public partial class MatrixMessageDispatchTestMessage
    {
    }

    [Aero]
    [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.SuperPong)]
    public partial class MatrixMessagePongDispatchTestMessage
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
            Assert.Greater(id, (byte)0, $"MatrixMessage.{message} should exist in {version}");
            Assert.AreEqual((int)message, MatrixTables.FindMessage(version, id));
            Assert.AreEqual(MatrixMessage.Announce, AeroRouting.GetMatrixMessage(version, id).Value);
            Assert.Less(MatrixTables.FindMessage(version, 0), 0);
        }

        [Test]
        public void MatrixTables_TryGetMessageId_MatchesGetMessageId()
        {
            const MatrixVersion version = MatrixVersion.V7;
            const MatrixMessage message = MatrixMessage.Announce;

            Assert.IsTrue(MatrixTables.TryGetMessageId(version, message, out byte id));
            Assert.AreEqual(MatrixTables.GetMessageId(version, message), id);
        }

        [Test]
        public void MatrixTables_TryGetMessageId_Absent_ReturnsFalse()
        {
            Assert.IsFalse(MatrixTables.TryGetMessageId(MatrixVersion.V7, MatrixMessage.UpdateDevZoneInfo, out byte id));
            Assert.AreEqual(0, id);
        }

        [Test]
        public void MatrixRouting_MsgSrc_SelectsRegisteredHandler()
        {
            const MatrixVersion version = MatrixVersion.V7;

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Command, MatrixMessage.SuperPing) is MatrixCommandDispatchTestMessage);
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Message, MatrixMessage.SuperPing) is MatrixMessageDispatchTestMessage);
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(version, MsgSrc.Message, MatrixMessage.SuperPong) is MatrixMessagePongDispatchTestMessage);
            Assert.IsNull(AeroRouting.GetNewMessageHandler(version, MsgSrc.Command, MatrixMessage.SuperPong));
        }

        [Test]
        public void MatrixRouting_WireIds_RespectMsgSrc()
        {
            const MatrixVersion version = MatrixVersion.V7;
            byte pingId = MatrixTables.GetMessageId(version, MatrixMessage.SuperPing);
            byte pongId = MatrixTables.GetMessageId(version, MatrixMessage.SuperPong);

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Command, pingId) is MatrixCommandDispatchTestMessage);
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Message, pingId) is MatrixMessageDispatchTestMessage);
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Message, pongId) is MatrixMessagePongDispatchTestMessage);
            Assert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.Matrix, MsgSrc.Command, pongId));
        }

        [Test]
        public void GssRoot_RoundTrip()
        {
            var ordinal = (int)GssMessage.RequestLogout;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Root, GssTables.Kind.Message, ordinal);

            Assert.AreEqual(GssTables.Ns.Root, GssTables.FindNamespace(version, typecode, GssTables.Kind.Message));
            Assert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Message, id));
            Assert.AreEqual(ordinal, AeroRouting.GetGssMessageOrdinal(version, typecode, GssTables.Kind.Message, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Root, GssTables.Kind.Message, ordinal);
            Assert.AreEqual(typecode, typecode2);
            Assert.AreEqual(id, id2);
        }

        [Test]
        public void GssCharacterMessage_RoundTrip()
        {
            var ordinal = (int)GssCharacterMessage.CharacterLoaded;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Message, ordinal);

            Assert.AreEqual(GssTables.Ns.Character, GssTables.FindNamespace(version, typecode, GssTables.Kind.Message));
            Assert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Message, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, ordinal);
            Assert.AreEqual(typecode, typecode2);
            Assert.AreEqual(id, id2);
        }

        [Test]
        public void GssCharacterCommand_RoundTrip()
        {
            var ordinal = (int)GssCharacterCommand.ActivateAbility;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Command, ordinal);

            Assert.AreEqual(GssTables.Ns.Character, GssTables.FindNamespace(version, typecode, GssTables.Kind.Command));
            Assert.AreEqual(ordinal, GssTables.FindMessage(version, typecode, GssTables.Kind.Command, id));

            var (typecode2, id2) = AeroRouting.GetGssMessageId(version, GssTables.Ns.Character, GssTables.Kind.Command, ordinal);
            Assert.AreEqual(typecode, typecode2);
            Assert.AreEqual(id, id2);
        }

        [Test]
        public void GssTables_TryGetWireIds_Message()
        {
            var ordinal = (int)GssCharacterMessage.CharacterLoaded;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Message, ordinal);

            Assert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterMessage.CharacterLoaded, out byte wireTypecode, out byte wireMessageId));
            Assert.AreEqual(typecode, wireTypecode);
            Assert.AreEqual(id, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_Command()
        {
            var ordinal = (int)GssCharacterCommand.ActivateAbility;
            var (version, typecode, id) = FindAvailable(GssTables.Ns.Character, GssTables.Kind.Command, ordinal);

            Assert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterCommand.ActivateAbility, out byte wireTypecode, out byte wireMessageId));
            Assert.AreEqual(typecode, wireTypecode);
            Assert.AreEqual(id, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_View()
        {
            var ordinal = (int)GssCharacterView.ObserverView;
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, ordinal);

            Assert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterView.ObserverView, out byte wireTypecode, out byte wireMessageId));
            Assert.AreEqual(typecode, wireTypecode);
            Assert.AreEqual(0, wireMessageId);
        }

        [Test]
        public void GssTables_TryGetWireIds_UnknownEnum_ReturnsFalse()
        {
            Assert.IsFalse(GssTables.TryGetWireIds(GssVersion.V1, 123, out byte typecode, out byte messageId));
            Assert.AreEqual(0, typecode);
            Assert.AreEqual(0, messageId);
        }

        [Test]
        public void GssTables_UnknownTypecode_IsUnknown()
        {
            Assert.AreEqual(GssTables.Ns.Unknown, GssTables.FindNamespace(GssVersion.V1, 255, GssTables.Kind.Message));
            Assert.AreEqual(GssTables.Ns.Unknown, GssTables.FindNamespace(GssVersion.V1, 255, GssTables.Kind.Command));
            Assert.Less(GssTables.FindMessage(GssVersion.V1, 255, GssTables.Kind.Message, 1), 0);
        }

        [Test]
        public void GssTables_TryFindView_RoundTrip()
        {
            var ordinal = (int)GssCharacterView.ObserverView;
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, ordinal);

            Assert.AreEqual(ordinal, GssTables.FindView(version, typecode));
            Assert.IsTrue(GssTables.TryFindView(version, typecode, out int ns, out int foundOrdinal));
            Assert.AreEqual(GssTables.Ns.Character, ns);
            Assert.AreEqual(ordinal, foundOrdinal);

            Assert.IsFalse(GssTables.TryFindView(version, 255, out int unknownNs, out int unknownOrdinal));
            Assert.AreEqual(GssTables.Ns.Unknown, unknownNs);
            Assert.AreEqual(-1, unknownOrdinal);
        }

        [Test]
        public void GssViewRouting_WireTypecode_ReturnsRegisteredView()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode) is GssViewDispatchTestController);
        }

        [Test]
        public void GssViewRouting_MessageId_IsIgnoredForSelection()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode) is GssViewDispatchTestController);
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 42, typecode) is GssViewDispatchTestController);
        }

        [Test]
        public void GssViewRouting_VersionRange_SelectsCorrectClass()
        {
            var earlyTypecode = GssTables.GetMessageId(GssVersion.V10, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var lateTypecode = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            Assert.Greater(earlyTypecode, (byte)0);
            Assert.Greater(lateTypecode, (byte)0);

            AeroRouting.CurrentGssVersion = GssVersion.V10;
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 0, earlyTypecode) is GssViewDispatchTestViewEarly);

            AeroRouting.CurrentGssVersion = GssVersion.V40;
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 0, lateTypecode) is GssViewDispatchTestViewLate);
        }

        [Test]
        public void GssViewRouting_CommandSrc_ReturnsNull()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.BaseController);
            AeroRouting.CurrentGssVersion = version;

            Assert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Command, 1, typecode));
        }

        [Test]
        public void GssViewRouting_UnknownTypecode_ReturnsNull()
        {
            AeroRouting.CurrentGssVersion = GssVersion.V1;
            Assert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, 255));
        }

        [Test]
        public void GssViewRouting_UnregisteredView_ReturnsNull()
        {
            var (version, typecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            AeroRouting.CurrentGssVersion = version;

            Assert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 1, typecode));
        }

        [Test]
        public void GssViewRouting_DirectEnumOverload()
        {
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterView.BaseController) is GssViewDispatchTestController);
            Assert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterView.BaseController));
        }

        [Test]
        public void GssViewRoutedMessage_TryGetWireIds_ReturnsViewRouteAndMessageId()
        {
            var (version, viewTypecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            var messageId = GssTables.GetMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);
            Assert.Greater(messageId, (byte)0);

            Assert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterView.CombatView, GssCharacterMessage.Killed, out byte wireTypecode, out byte wireMessageId));
            Assert.AreEqual(viewTypecode, wireTypecode);
            Assert.AreEqual(messageId, wireMessageId);

            // The view-less lookup returns the namespace route's typecode for the same message.
            Assert.IsTrue(GssTables.TryGetWireIds(version, GssCharacterMessage.Killed, out byte nsTypecode, out byte nsMessageId));
            Assert.AreEqual(messageId, nsMessageId);
            Assert.AreNotEqual(viewTypecode, nsTypecode);
        }

        [Test]
        public void GssViewRoutedMessage_WireDispatch_ReturnsRegisteredClass()
        {
            var (version, viewTypecode) = FindAvailableView(GssTables.Ns.Character, (int)GssCharacterView.CombatView);
            var messageId = GssTables.GetMessageId(version, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, messageId, viewTypecode) is GssViewRoutedKilledViaCombatView);
        }

        [Test]
        public void GssViewRoutedMessage_NamespaceRoute_DoesNotMatch()
        {
            // Killed is registered via the CombatView route, so the plain Character namespace route must not dispatch it.
            var nsTypecode = GssTables.GetNamespaceTypecode(GssVersion.V1, GssTables.Ns.Character);
            var messageId = GssTables.GetMessageId(GssVersion.V1, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.Killed);
            Assert.Greater(nsTypecode, (byte)0);
            Assert.Greater(messageId, (byte)0);

            Assert.IsNull(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, messageId, nsTypecode));
        }

        [Test]
        public void GssViewRoutedMessage_VersionRange_SelectsCorrectClass()
        {
            var earlyTypecode = GssTables.GetMessageId(GssVersion.V20, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var lateTypecode = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.View, (int)GssCharacterView.ObserverView);
            var earlyId = GssTables.GetMessageId(GssVersion.V20, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.CharacterLoaded);
            var lateId = GssTables.GetMessageId(GssVersion.V40, GssTables.Ns.Character, GssTables.Kind.Message, (int)GssCharacterMessage.CharacterLoaded);
            Assert.Greater(earlyTypecode, (byte)0);
            Assert.Greater(lateTypecode, (byte)0);
            Assert.Greater(earlyId, (byte)0);
            Assert.Greater(lateId, (byte)0);

            try
            {
                AeroRouting.CurrentGssVersion = GssVersion.V20;
                Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, earlyId, earlyTypecode) is GssViewRoutedLoadedEarly);

                AeroRouting.CurrentGssVersion = GssVersion.V40;
                Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, lateId, lateTypecode) is GssViewRoutedLoadedLate);
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
            Assert.Greater(commandId, (byte)0);

            Assert.IsTrue(AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Command, commandId, viewTypecode) is GssViewRoutedActivateAbilityViaBaseController);
        }

        [Test]
        public void GssViewRoutedMessage_DirectOverload()
        {
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterMessage.Killed, GssCharacterView.CombatView) is GssViewRoutedKilledViaCombatView);
            Assert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Message, GssCharacterMessage.Killed));
        }

        [Test]
        public void GssViewRoutedCommand_DirectOverload()
        {
            Assert.IsTrue(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterCommand.ActivateAbility, GssCharacterView.BaseController) is GssViewRoutedActivateAbilityViaBaseController);
            Assert.IsNull(AeroRouting.GetNewMessageHandler(GssVersion.V1, MsgSrc.Command, GssCharacterCommand.ActivateAbility));
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
