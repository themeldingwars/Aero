using System;

namespace Aero.Gen.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AeroMessageIdAttribute : Attribute
    {
        public static string Name = "AeroMessageId";

        public MsgType Typ;
        public MsgSrc  Src;
        public int     ControllerId;
        public int     MessageId;

        public string FullClassName;
        
        public AeroMessageIdAttribute(MsgType typ, MsgSrc src, int controllerId, int messageId)
        {
            Typ          = typ;
            Src          = src;
            ControllerId = controllerId;
            MessageId    = messageId;
        }
        
        public AeroMessageIdAttribute(MsgType typ, MsgSrc src, int messageId)
        {
            Typ       = typ;
            Src       = src;
            MessageId = messageId;
        }

        public string GetAsString()
        {
            var str = $"{Typ}_{Src}_{ControllerId}_{MessageId}";
            return str;
        }

        public enum MsgType : byte
        {
            Control,
            Matrix,
            GSS
        }

        public enum MsgSrc : byte
        {
            Command,
            Message,
            Both
        }
    }
}