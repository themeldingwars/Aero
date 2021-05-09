<img width="150px" src="https://static.wikia.nocookie.net/firefall_gamepedia/images/a/ad/Aero.png/revision/latest/scale-to-width-down/256?cb=20151109215556" />

# Aero
## Packet and file serialization code gen
For the server we are hoping to have a code gen to create the packet reader and writers for the messages and maybe to also be used on the file formats.
The idea is you define the class and annotate the fields and then have the code gen generate the read and write functions, the benfit of this is avoid having to write uguly error prone code over and over again and allows us to make sweeping changes to the readers and writers with out having to re write all those functions.
Planning to use .Net 5 Source Generators. <https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/>

An other plus from this should be better packet peep inspection, the code gen can also create imgui views for each class with better views on the data and better tracking of reads.

Why Aero, well she did talk alot so....

# Key points
* Can have multiple serializers, eg we might use Bitter for now and change to a more span based reader
* Should be able to log reads and offset as an optional setting for Packet Peep or other debuggers / inspectors.
* Generated code can be long and ugly and that's ok! Sub types can be unrolled into the calling reader to avoid function call over head and other such microish optimisations with out having the normal negative impact on code readability.
* Classes should be marked partial and extend from AeroBase. (partial to allow extending with the readers, writer and inspectors).
* Classes should be marked with the [Aero] attribute.
* We can have a testing setup to run the packet captures through, the result of reading and then writing back should match.
    
# Attributes
## ``[Aero]``
Marks a class as one that should have readers, writers and such generated for it.
* Different type ares `AeroType.Msg`, `AeroType.Block`, `AeroType.File`
  * `AeroType.Msg` is for network messages
  * `AeroType.File` is for file types
  * `AeroType.Block` is for a sub type that can be found in a `AeroType.Msg`, `AeroType.File`, or even another `AeroType.Block`
* The message types in the ``AeroMsgType`` enum are ``Control``, ``Matrix``, ``GSS``, these are mostly just meta data
* ``AeroSrc`` is the source of the message and can be ``Server``, ``Client``, ``Both``
* There are overloaded constructors for the different packet types, eg. Matrix, GSS
  * Control: ``Aero(AeroType.Msg, AeroMsgType.Control, 1, Ver: 1946)``
  * Matrix: ``Aero(AeroType.Msg, AeroMsgType.Matrix, 1, Ver: 1946)``
  * GSS: ``Aero(AeroType.Msg, AeroMsgType.GSS, AeroSrc.Server, 2, 187, Ver: 1946)``
* ``Ver`` is the first known version that this message is good for, so from this to the next ver found for this message
  

## ``[AeroArray]``
Marks a field as an array, there are a few variants of this.
* ``AeroArray(uint length)`` : eg ``[AeroArray(2)]`` Will read 2 values of the type of the array this is attached to
    * if the length is -1 then it will read until the end of the current buffer.
* ``AeroArray(string nameOfFeild)``: like the normal use only will take the length from the named field.
    * The named field must be a number type, eg, byte, short, int
    * Should use ``nameof`` eg ``[AeroArray(nameof(ArrayLen))]``
* ``AeroArray(Type lengthType)`` read a number type of that type and use that for the length of the array
  * eg. ``AeroArray(typeof(uint))`` Will read a uint and then read that value number of elements.

## ``[AeroNullTermString]``
Read the string until it finds a null term char.

## ``[AeroFixedString]``
Read a fixed sized string.
* ``AeroFixedString(uint length)`` : eg ``[AeroFixedString(2)]`` Will read 2 chars into the string

## ``[AeroSubType]``
Based on a check, use a specific subclass of this field to read / write  and assign
* ``AeroSubType(string keyName, string values, TypesubClassType)`` : eg ``[AeroSubType("Key", "Type1", typeof(Type1))]`` Will use Type1 if the value in key == "Type1"
  * values can be an array of values in the format ``"type1,type2"`` ``,`` delimited, the type will be cast to the type of the field referenced by keyName

## ``[AeroIf]``
Only serlise this feild if this check is matched
* ``AeroIf(string nameOfValue, var valueToMatch)`` : eg ``[AeroFixedString(nameof(Value), "Yes")]`` if the field Value is a string and matches "Yes" then this field will be serialised

## ``[AeroSDB]`` *
Link this value to an SDB Table and row using this value as an id
* ``AeroSDB(uint tableID, uint columID)``

# Notes
* For some more complex types where serialisation depends on an other value like a flag I think for simplicity its best not to have this lib try and set those flags based on what data is set.
Rather that's better up to constructors on the class definition itself.

# Examples
Some examples of the class and then the generated pseudo code. (The outputted code I hope would be more efficient than this)
If you see any issues or know of some packets / messages that will be tricky to parse or write let me know.

## Control.TimeSyncResponse
### Class Define
```csharp
[Aero(AeroType.Msg, MsgType.Control, 5, Vers: new [] { 1942, 1946 })]
public partial class TimeSyncResponse : AeroBase
{
    public ulong ClientTime; // Microseconds Client System Uptime (hrtime)
    public ulong ServerTime; // Microseconds UNIX Epoch
}
```

### Generated Reader / Writer
```csharp
public partial class TimeSyncResponse : AeroBase
{
    public void Read(byte[] data)
    {
        int offset = 0;
        ClientTime = ReadUint(data, ref offset);
        ServerTime = ReadUint(data, ref offset);
    }
    
    public byte[] Write()
    {
        var buffer = new AeroBuffer();
        buffer.WriteUint(ClientTime);
        buffer.WriteUint(ServerTime);
        
        return buffer.ToArray();
    }
}
```

## Matrix.Login
### Class Define
```csharp
[Aero(AeroType.Msg, MsgType.Matrix, 17, Ver: 1946)]
public partial class Login : AeroBase
{
    public byte Unk1;
    public ushort ClientVersion;
    [AeroArray(3)]
    public byte[] Unk2;
    public ulong CharacterGUID;
    [AeroArray(13)]
    public byte[] Unk3;
    [AeroNullTermString]
    public string Red5Sig2; // From Web Requests to ClientAPI
    public byte Unk4;
    [AeroArray(-1)]
    public byte[] Red5Sig1; // From Web Requests to ClientAPI
}
```

### Generated Reader / Writer
```csharp
public partial class Login : AeroBase
{
    public void Read(byte[] data)
    {
        int offset = 0;
        Unk1 = ReadByte(data, ref offset);
        ClientVersion = ReadUshort(data, ref offset);
        Unk2 = ReadArray<byte>(data, 3, ref offset);
        CharacterGUID =  ReadUlong(data, ref offset);
        Unk3 = ReadArray<byte>(data, 13, ref offset);
        Red5Sig2 = ReadNullTermString(data, ref offset);
        
        int Red5Sig1_len = data.length - offset;
        Red5Sig1 = ReadArray<byte>(data, Red5Sig1_len, ref offset);
    }
    
    public byte[] Write()
    {
        var buffer = AeroBuffer();
        buffer.WriteByte(Unk1);
        buffer.WriteUshort(ClientVersion);
        buffer.WriteByteArray(Unk2); // Should we asset if the lenght written doesn't match the defenation?
        buffer.WriteUlong(CharacterGUID);
        buffer.WriteByteArray(Unk3);
        buffer.WriteNullTermString(Red5Sig2);
        buffer.WriteByteArray(Red5Sig1);
    }
}
```

## GSS.PostStatEvent
### Class Define
```csharp
[Aero(AeroType.Msg, MsgType.GSS, AeroSrc.Server, 2, 184, Ver: 1946)]
public partial class PostStatEvent : AeroBase
{
    [AeroSDB("dbstats::Stat", "Id")] // Not needed but could be nice to link for extra data views in packet peep or other inspectors?
    public uint StatEventId;
    [AeroArray(typeof(uint))]
    public StatData[] Stats;
}

[Aero(AeroType.Block]
public partial class StatData : AeroBase
{
    [AeroNullTermString]
    public string Key;
    [AeroSubType(nameof(Key), "FrameTypeId,Volume,DeathStreak,AbilityId,Distance", typeof(StatDataType0))]
    [AeroSubType(nameof(Key), "Value", typeof(StatDataType1))]
    [AeroSubType(nameof(Key), "PlayerGUID,TargetGUID", typeof(StatDataType2))] // If a case is unhandled and there isn't a default handler should we throw and error?
    public StatDataBase Data;
}

[Aero(AeroType.Block]
public partial class StatDataBase : AeroBase
{
    
}

[Aero(AeroType.Block]
public partial class StatDataType0 : StatDataBase
{
    public uint Unk1;
    public uint Id;
}

[Aero(AeroType.Block]
public partial class StatDataType1 : StatDataBase
{
    [AeroArray(8)]
    public byte[] Unk;
}

[Aero(AeroType.Block]
public partial class StatDataType2 : StatDataBase
{
    public uint Unk1;
    public ulong EntityId;
}
```

### Generated Reader / Writer
```csharp
public partial class PostStatEvent : AeroBase
{
    public void Read(byte[] data)
    {
        int offset = 0;
    }
    
    public byte[] Write()
    {
        var buffer = new AeroBuffer();
        
        return buffer.ToArray();
    }
}
```

## GSS.ConfirmedPoseUpdate
### Class Define
```csharp
[Aero(AeroType.Msg, MsgType.GSS, 2, 111, Ver: 1946)]
public partial class ConfirmedPoseUpdate : AeroBase
{
    [Flags]
    public Enum PoseType : byte
    {
        Velocity = 0,
        PosAndRot = 1,
        Aim = 2
    }
    
    [Aero(AeroType.Block)]
    public struct PosAndRotData
    {
        public Vector3 Pos;
        public Quaterion Rot;
        public short MovementState;
    }

    public ushort ShortTime1;
    public PoseType Flags;
    public byte Unk3;
    
    [AeroIf(nameof(Flags), PoseType.PosAndRot)]
    public PosAndRotData PosAndRot;
    
    public Vector3 Velocity;
    
    [AeroIf(nameof(Flags), PoseType.Aim)]
    public Vector3 Aim;
    
    public ushort Unk5;
    public short GroundTimePositiveAirTimeNegative;
    public short TimeSinceLastJump;
}
```

### Generated Reader / Writer
```csharp
public partial class ConfirmedPoseUpdate : AeroBase
{
    public void Read(byte[] data)
    {
        int offset = 0;
        ShortTime1 = ReadUshort(data, ref offset);
        Flags = (PoseType)ReadByte(data, ref offset);
        Unk3 = ReadByte(data, ref offset);
        
        if (Flags.HasFlag(PoseType.PosAndRot))
        {
            PosAndRot.Pos = ReadVector3(data, ref offset); // Some basic types liek Vector3s will have custom readers / writers written for them
            PosAndRot.Rot = ReadQuat(data, ref offset);
            PosAndRot.MovementState = Readshort(data, ref offset);
        }
        
        PosAndRot.Pos = ReadVector3(data, ref offset);
        
        if (Flags.HasFlag(PoseType.Aim))
        {
            Aim = ReadVector3(data, ref offset);
        }
        
        Unk5 = ReadUshort(data, ref offset);
        GroundTimePositiveAirTimeNegative = ReadShort(data, ref offset);
        TimeSinceLastJump = ReadShort(data, ref offset);
    }
    
    public byte[] Write()
    {
        var buffer = new AeroBuffer();   
        
        if (Flags.HasFlag(PoseType.PosAndRot))
        {

        }    
        
        if (Flags.HasFlag(PoseType.Aim))
        {
            
        }
        
        return buffer.ToArray();
    }
}
```