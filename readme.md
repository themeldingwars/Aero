# Aero
## Packet and file serialization code gen
For the server we are hoping to have a code gen to create the packet reader and writers for the messages and maybe to also be used on the file formats.
The idea is you define the class and annotate the fields and then have the code gen generate the read and write functions, the benfit of this is avoid having to write uguly error prone code over and over again and allows us to make sweeping changes to the readers and writers with out having to re write all those functions.
Planning to use .Net 5 Source Generators. <https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/>

An other plus from this should be better packet peep inspection, the code gen can also create imgui views for each class with better views on the data and better tracking of reads.

Why Aero, well she did talk alot so....

# How to install
Install the nuget package ``Aero.Gen``

# Generated Functions
The following functions will be generated on Aero marked classes

## Unpack:
``int Unpack(ReadOnlySpan<byte> data)``

Unpacks the given span into the class, the return value is how many bytes were read from the data span.</br>
If the return int is negative then the unpack failed and that how many bytes were read. (Needs to be compiled with bounds check on)
```csharp
var TestPacket = new TestPacket();
int readBytes = TestPacket.Unpack(packetData);
```

## GetPackedSize:
``int GetPackedSize()``
Calculate the size in bytes this class will have when packed.</br>
This function will preform much the same logic used for packing but just record the size.

```csharp
var TestPacket = new TestPacket();
int SizeThatWillBePacked = TestPacket.GetPackedSize();
```

## Pack:
``int Pack(Span<byte> buffer)``
Packs this class into the buffer and returns how many bytes where packed.
The given buffer span should be atleast big enough to pack all the fields into.

```csharp
Span<byte> buffer = new byte[10000];
var TestPacket = new TestPacket();
int packedSize = TestPacket.Pack(buffer);
```

# Supported Types
* byte
* char
* int
* uint
* long
* ulong
* short
* ushort
* float
* double
* string
* Vector2
* Vector3
* Vector4
* Quaternion

# Attributes
## ``[Aero]``
Marks a class as one that should have readers, writers and such generated for it.

## ``[AeroBlock]``
Marks a struct as one that can be serialised and included in an Aero class.

If a struct isn't marked with this it won't be serialised.

## ``[AeroArray]``
Marks a field as an array, there are a few variants of this.
* ``AeroArray(int length)`` : eg ``[AeroArray(2)]`` Will read 2 values of the type of the array this is attached to
* ``AeroArray(string nameOfFeild)``: like the normal use only will take the length from the named field.
    * The named field must be a number type, eg, byte, short, int
    * Should use ``nameof`` eg ``[AeroArray(nameof(ArrayLen))]``
* ``AeroArray(Type lengthType)`` read a number type of that type and use that for the length of the array
    * eg. ``AeroArray(typeof(uint))`` Will read a uint and then read that value number of elements.
* ``AeroArray(int -length)`` : eg ``[AeroArray(-4)]`` If the fixed size is negative then the array will be crated with that number positive but will keep reading untill the end of the data is reached
  * eg. ``[AeroArray(-4)] public int Test;`` will create an array of ints with a size of 4 and do a ``while(!hasReachedTheEnd)`` for reading
  * The size given as the arg should be the max size that the array can have, it won't be resized
  * There will be a ``Get[ArrayName]Count`` getter added that has the number of items that were read for this array.

## ``[AeroIf]``
A field with this will be conditionally serialised if the logic passes.
* ``[AeroIf(nameof(TestValue), 1)]``: Equivalent to ``if (TestValue == 1)`` around the read
* ``[AeroIf(nameof(TestValue), 1, 2)]``: Equivalent to ``if (TestValue == 1 || TestValue == 2)`` around the read
* ``[AeroIf(nameof(TestValue), Op.NotEqual, 1)]``: Equivalent to ``if (TestValue != 1)`` around the read
* ``[AeroIf(nameof(TestValue), Op.HasFlag, Flags.Flag1)]``: Equivalent to ``if (TestValue & Flags.Flag1)`` around the read

Multiple values in the one attribute will be ored as you can see above, to get and logic do like below:
```csharp
[AeroIf(nameof(TestValue), 1)]
[AeroIf(nameof(TestValue), 2)]
public int TestInt;
```

and creates code equivalent to:
```csharp
if ((Byte == 0) && (Byte == 1))
{

}
```

The ops options are:
* Equal
* NotEqual
* HasFlag
* DoesntHaveFlag

## ``[AeroString]``
Strings should be marked with this attribute to be parsed correctly.
* ``[AeroString(nameof(LenghtField))]``: Similar to the array attribute you can pass the name of a already defined numeric value to be used as the length of the string
* ``[AeroString(typeof(byte))]``: Marks the string as length prefixed with the given type, eg will read a byte and then that number of chars into the string
* ``[AeroString(10)]``: Read a fixed size string, in this case will read 10 chars as a string
* ``[AeroString]``: Defines a null terminated string, will read until it gets a ``0x00`` or it reaches the end of the span.

## ``[AeroSdb]``
* ``[AeroSdb("Table Name", "Column Name")]``: Mark a field as having a value from the sdb table and column given.

## ``[AeroMessageId]``
Marks a class as a routable message. When any class in the project uses this attribute, the generator also emits an `AeroRouting` helper.

* Control messages use a plain numeric id:
  * ``[AeroMessageId(MsgType.Control, MsgSrc.Message, 5)]``
* Matrix and GSS messages use the generated protocol enums and optional version ranges:
  ```csharp
  using Aero.Gen.Attributes;
  using Aero.Protocol;
  using static Aero.Gen.Attributes.AeroMessageIdAttribute;

  [Aero]
  [AeroMessageId(MsgType.Matrix, MsgSrc.Message, MatrixMessage.Login)]
  public partial class MyMatrixLogin { }

  [Aero]
  [AeroMessageId(MsgType.GSS, MsgSrc.Command, GssCharacterCommand.RequestLogout)]
  public partial class MyCharacterLogout { }

  [Aero]
  [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterMessage.CharacterLoaded, GssVersion.V11, GssVersion.V67)]
  public partial class MyCharacterLoaded { }

  // A message sent through a view: the wire typecode is the view route's id, not the namespace's.
  [Aero]
  [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterMessage.Killed, GssCharacterView.CombatView)]
  public partial class MyKilled { }

  [Aero(AeroGenTypes.View)]
  [AeroMessageId(MsgType.GSS, MsgSrc.Message, GssCharacterView.ObserverView)]
  public partial class MyCharacterObserverView { }
  ```
* GSS messages/commands can specify the view they are sent through with the version-agnostic `Gss*View` enum. Without a view the message is registered for the plain namespace route; with a view it is only dispatched on (and sent with) that view's route typecode. The view must exist in every version of the range, otherwise it's a generator error.
* Version ranges are optional. Omitted `from` is the first known protocol version, omitted `to` is the last known protocol version.
* `MatrixVersion`/`GssVersion` are ordered chronologically (V1 is the oldest). The raw protocol numbers are opaque, so the ordering is derived from the earliest client build (Patches dumps) that used each version; `ProtocolVersions.MatrixRaw`/`GssRaw` map the enum to raw numbers and `ProtocolVersions.MatrixFirstBuild`/`GssFirstBuild` show each version's first build time.
* `MsgSrc.Command` maps to GSS `Commands`, `MsgSrc.Message` maps to GSS `Messages`, and `MsgSrc.Both` registers the class for both where valid.
* Matrix uses one shared `MatrixMessage` id space; `MsgSrc` is dispatch/registration metadata only and does not change the Matrix wire id.
* Root `GssMessage` entries are message-only; GSS command enums must be used with `MsgSrc.Command`.
* `Gss*View` enums mark view classes for routing by their view/controller `typecode` (views are server -> client, so `MsgSrc.Command` is not allowed).
* Duplicate message ids or overlapping version ranges for the same protocol source are generator errors.

### Protocol lookup
The generated protocol tables expose wire-id lookup helpers. GSS lookup uses `typecode` in the public API:

```csharp
using Aero.Protocol;

GssTables.TryGetWireIds(GssVersion.V15, GssCharacterMessage.CharacterLoaded, out byte typecode, out byte messageId);
GssTables.TryGetWireIds(GssVersion.V15, GssCharacterCommand.ActivateAbility, out byte typecode, out byte messageId);
GssTables.TryGetWireIds(GssVersion.V15, GssCharacterView.ObserverView, out byte typecode, out byte messageId);

// A message sent through a view: typecode is the view route's id.
GssTables.TryGetWireIds(GssVersion.V15, GssCharacterView.CombatView, GssCharacterMessage.Killed, out byte typecode, out byte messageId);

MatrixTables.TryGetMessageId(MatrixVersion.V1, MatrixMessage.Login, out byte messageId);
```

* For GSS messages and commands, `typecode` is the namespace typecode and `messageId` is the message/command wire id.
* For GSS messages/commands sent through a view, use the `(view, message)` overload; `typecode` is then the view route's typecode.
* For GSS views, `typecode` is the view/controller typecode and `messageId` is `0`.
* Matrix lookup does not expose a typecode because Matrix has a single message id space.
* `TryGet...` returns `false` when the protocol enum is unknown, the value is absent in that version, or the GSS namespace is not routed in that version.

# Examples
Here are some exampls, for more you can see the unit tests in the project.

## Example 1
A basic example for all that is needed to have basic value types serialised

```csharp
[Aero]
public partial class SimpleTypes
{
    public byte   Byte;
    public char   Char;
    public int    IntTest;
    public uint   UintTest;
    public short  ShortTest;
    public ushort UshortTest;
    public long   Long;
    public ulong  ULong;
    public float  Float;
    public double Double;
}
```

## Example 2
A basic example with arrays and some logic

```csharp
[Aero]
public partial class Example2
{
    public byte   Byte;
    public char   Char;
    
    // Read 4 ints
    [AeroArray(4)]
    public int[] FourInts;
    
    // Read an int and uses it to get the length of the array and then reads them
    [AeroArray(typeof(int))]
    public int[] VarableIntArray;
    
    // If the Byte value is 1 then the value is read, other wise it isn't
    [AeroIf(nameof(Byte), 1)]
    public int ShouldBeReadIfByteIs1;
}
```

## Example 3
A basic example of string parsing

```csharp
[Aero]
public partial class Example3
{
  [AeroString]
  public string NulNullTerminatedString;
  
  [AeroString(10)]
  public string FixedSizeString;
  
  [AeroString(typeof(byte))]
  public string ByteLengthPrefixedString;
  
  public int StringLength;
  [AeroString(nameof(StringLength))]
  public string VarablePrefixedLengthString;
}
```

# Views
To mark a class as a view add the attribute ``[Aero(AeroGenTypes.View)]`` or ``[Aero(AeroGenTypes.Controller)]`` to it, the true marks it as a view and will genrate the extra data.

Fields in the class will get a ``ShadowField`` idx based on the order that they are defined in.
Nullables can be marked as such with the ``[AeroNullable]``

Example:
```csharp
[Aero(AeroGenTypes.View)]
public partial class SimpleViewWithNullable
{
    public int   TestValue;
    public float TestVlaue2;
    
    [AeroNullable] public int TestValueNullable;
}
```

The added functions are:
* ``GetPackedSize()``
* ``UnpackChanges(ReadOnlySpan<byte> data)``
* ``PackChanges(Span<byte> buffer, bool clearDirtyAfterSend = true)``
* ``ShadowFieldIdToName(int id)`` Returns a `string` name for the shadow field with this id
* ``ShadowFieldIdToType(int id)`` Returns the `Type` for the shadow field with this id
* ``GetShadowFieldsData()`` Returns an array of `(string, int, Type, bool)` for all the shadowfields in this class, (name, id, Type, nullable)

If compiled with the Diag Logging enabled then there will also be a function ``GetDiagReadLogs()`` that will return a `List<AeroReadLog>` for each read that was done in the ``Unpack`` or ``UnpackChanges`` functions.

For each field in a view call a property will be genrated, for non nullables the set on this will mark that value as dirty so a ``PackChanges`` call will only pack what has changed.
This is why feilds should be defined as private to ensure only the propetys can be called and those changes can be tracked.
These views shouldn't be shared or polled for this reason.


# Message routing
If any class in the project has ``[AeroMessageId]`` the generator emits a public static class ``AeroRouting``.

Set the current protocol versions before resolving wire ids:
```csharp
AeroRouting.CurrentMatrixVersion = MatrixVersion.V1;
AeroRouting.CurrentGssVersion = GssVersion.V1;
```

Resolve a message instance from wire data:
```csharp
IAero msg = AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 12, 3);
```

`routeId` is only meaningful for GSS and is the namespace route id. Matrix uses `0`.
Direct enum overloads are also generated for registered protocol enums:
```csharp
IAero msg = AeroRouting.GetNewMessageHandler(GssVersion.V6, MsgSrc.Message, GssCharacterMessage.CharacterLoaded);
```

View routes use the same entry point: when the `typecode` is a view/controller route id, the router returns the view class registered for it (the `messageId` is not used for selection - the consumer interprets it in the view's context). The `typecode` -> view mapping for a protocol version comes from the generated `GssTables`:
```csharp
IAero view = AeroRouting.GetNewMessageHandler(MsgType.GSS, MsgSrc.Message, 7, viewTypecode);
// or by enum:
IAero view = AeroRouting.GetNewMessageHandler(GssVersion.V6, MsgSrc.Message, GssCharacterView.ObserverView);

GssTables.TryFindView(GssVersion.V6, viewTypecode, out int nsIndex, out int viewOrdinal);
```

# Protocol tables
Matrix and GSS protocol enums, version enums, patch mappings, and the Matrix/GSS ``[AeroMessageId]`` overloads are generated by ``Aero.ProtocolGen`` from Sift protocol dumps.

The generated files are checked in under `Aero.Gen/Protocol/` and are shipped inside the `Aero.Gen` package. Contributors can regenerate them with:
```bash
dotnet run --project Aero.ProtocolGen -c Release
```

The defaults read from `Aero.Gen/Lib/Sift/Dumps` and write to `Aero.Gen/Protocol`. Commit the generated files if the Sift dumps change.

# Config
The following settings can be used in a `.globalconfig` file to adjust the generator's output.
* ``Aero_Enabled``: Enable or disable the generator
* ``Aero_BoundsCheck``: Enable or disable bounds checking for the unpacker, will return -bytes read if it couldn't read more from the passed buffer
* ``Aero_DiagLogging``: Enable or disable diagnostic logging from the packer / unpackers
* ``Aero_LogReadsWrites``: Enable or disable logging the reading or writing done by the packers or unpackers (just the unpackers atm), needs Aero_DiagLogging enabled too.