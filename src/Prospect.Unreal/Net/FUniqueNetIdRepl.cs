using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Serialization;
using Serilog;

namespace Prospect.Unreal.Net;

public class FUniqueNetIdRepl
{
    private static readonly ILogger Logger = Log.ForContext<FUniqueNetIdRepl>();
    private const int TypeHashOther = 31;
    
    public FUniqueNetId? UniqueNetId { get; private set; }

    public FBitWriter ReplicationBytes { get; private set; }

    public bool IsValid()
    {
        return UniqueNetId != null && UniqueNetId.IsValid();
    }

    public string ToDebugString()
    {
        return IsValid() ? $"${UniqueNetId!.Type}:{UniqueNetId!.Contents}" : "INVALID";
    }
    
    public static void Write(FArchive ar, FUniqueNetIdRepl value)
    {
        Serialize(ar, value);
    }

    public static FUniqueNetIdRepl Read(FArchive ar)
    {
        var result = new FUniqueNetIdRepl();
        Serialize(ar, result);
        return result;
    }

    private void MakeReplicationData()
    {
        var Contents = "";
        if (IsValid())
        {
            //Contents = UniqueNetId->ToString();
        }
        Contents = "DESKTOP-D9VF075-1E4508FB45033E32A85E18839C171C43";
        const byte TypeHash_Other = 31;
        int Length = Contents.Length;
        if (Length > 0)
        {
            // For now don't allow odd chars (HexToBytes adds a 0)
            bool bEvenChars = (Length % 2) == 0;
            var EncodedSize32 = ((Length * sizeof(char)) + 1) / 2;
            var bIsNumeric = Contents.All(char.IsNumber) && !(Contents.StartsWith("+") || Contents.StartsWith("-"));
            var bIsPadded = bIsNumeric && !bEvenChars;

            //UE_LOG(LogNet, VeryVerbose, TEXT("bEvenChars: %d EncodedSize: %d bIsNumeric: %d bIsPadded: %d"), bEvenChars, EncodedSize32, bIsNumeric, bIsPadded);

            var encodingFlags = (bIsNumeric || (bEvenChars && (EncodedSize32 < byte.MaxValue))) ? EUniqueIdEncodingFlags.IsEncoded : EUniqueIdEncodingFlags.NotEncoded;
            if (bIsPadded)
            {
                encodingFlags |= EUniqueIdEncodingFlags.IsPadded;
            }

            if (encodingFlags.HasFlag(EUniqueIdEncodingFlags.IsEncoded) && !bIsNumeric)
            {
                for (var i = 0; i < Length; ++i)
                {
                    // Don't allow uppercase because HexToBytes loses case and we aren't encoding anything but all lowercase hex right now
                    if (!Uri.IsHexDigit(Contents[i]) || char.IsUpper(Contents[i]))
                    {
                        encodingFlags = EUniqueIdEncodingFlags.NotEncoded;
                        break;
                    }
                }
            }

            // Encode the unique id type
            //FName Type = GetType();
            FName Type = new FName(EName.Name);
            //var TypeHash = UOnlineEngineInterface::Get()->GetReplicationHashForSubsystem(Type);
            var TypeHash = 1;// need to fix... temporary for null
            if (TypeHash == 0 && Type != EName.None)
            {
                TypeHash = 0;// TypeHash_Other;
            }
            encodingFlags = (EUniqueIdEncodingFlags)((TypeHash << 3) | (byte)encodingFlags);

            if (encodingFlags.HasFlag(EUniqueIdEncodingFlags.IsEncoded))
            {
                var EncodedSize = (byte)EncodedSize32;
                var TotalBytes = sizeof(EUniqueIdEncodingFlags) + 1 + EncodedSize;
                ReplicationBytes = new FBitWriter(TotalBytes);
                ReplicationBytes.WriteByte((byte)encodingFlags);
                if (TypeHash == TypeHash_Other)
                {
                    ReplicationBytes.WriteString(Type.ToString());
                }
                ReplicationBytes.WriteByte(EncodedSize);

                var HexStartOffset = ReplicationBytes.Tell();
                var hexBytes = Enumerable.Range(0, Contents.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(Contents.Substring(x, 2), 16)).ToArray();
                ReplicationBytes.Serialize(hexBytes, hexBytes.Length);
                //Writer.Seek(HexStartOffset + HexEncodeLength);
                //UE_LOG(LogNet, VeryVerbose, TEXT("HexEncoded UniqueId, serializing %d bytes"), ReplicationBytes.Num());
            }
            else
            {
                ReplicationBytes = new FBitWriter(Length * 8 + 8 + 8 + 32);
                ReplicationBytes.WriteByte((byte)encodingFlags);
                
                if (TypeHash == TypeHash_Other)
                {
                    ReplicationBytes.WriteString(Type.ToString());
                }
                ReplicationBytes.WriteString(Contents);
                //UE_LOG(LogNet, VeryVerbose, TEXT("Normal UniqueId, serializing %d bytes"), ReplicationBytes.Num());
            }
        }
        else
        {
            var encodingFlags = (EUniqueIdEncodingFlags.IsEncoded | EUniqueIdEncodingFlags.IsEmpty);

            ReplicationBytes = new FBitWriter(8);
            ReplicationBytes.WriteByte((byte)encodingFlags);
            //UE_LOG(LogNet, VeryVerbose, TEXT("Empty/Invalid UniqueId, serializing %d bytes"), ReplicationBytes.Num());
        }
    }

    private void NetSerialize(FArchive ar, UPackageMap? packageMap, out bool bOutSuccess)
    {
        // TODO: Get back to this later when we understand FName / FNamePool and UOnlineEngineInterface better.
        // FUniqueNetIdRepl::NetSerialize
        
        bOutSuccess = false;
        
        if (ar.IsSaving())
        {
            if (ReplicationBytes == null || ReplicationBytes.Num == 0)
            {
                MakeReplicationData();
            }
            
            ar.Serialize(ReplicationBytes.GetData(), ReplicationBytes.GetNumBytes());
            //bOutSuccess = (ReplicationBytes.Num() > 0);
        } 
        else if (ar.IsLoading())
        {
            UniqueNetId = null;
            
            var encodingFlags = (EUniqueIdEncodingFlags)ar.ReadByte();

            if (!ar.IsError())
            {
                if (encodingFlags.HasFlag(EUniqueIdEncodingFlags.IsEncoded))
                {
                    if (!encodingFlags.HasFlag(EUniqueIdEncodingFlags.IsEmpty))
                    {
                        // Non empty and hex encoded
                        var typeHash = GetTypeHashFromEncoding(encodingFlags);
                        if (typeHash == 0)
                        {
                            // If no type was encoded, assume default
                            throw new NotImplementedException();
                            // TypeHash = UOnlineEngineInterface::Get()->GetReplicationHashForSubsystem(UOnlineEngineInterface::Get()->GetDefaultOnlineSubsystemName());
                        }

                        var bValidTypeHash = typeHash != 0;
                        
                        if (typeHash == TypeHashOther)
                        {
                            var typeString = ar.ReadString();
                            var type = new FName(typeString);
                            throw new NotImplementedException();
                        }
                        else
                        {
                            // Type = UOnlineEngineInterface::Get()->GetSubsystemFromReplicationHash(TypeHash);
                        }

                        throw new NotImplementedException();
                    }
                    else
                    {
                        bOutSuccess = true;
                    }
                }
                else
                {
                    // Original FString serialization goes here
                    var typeHash = GetTypeHashFromEncoding(encodingFlags);
                    if (typeHash == 0)
                    {
                        // If no type was encoded, assume default
                        //throw new NotImplementedException();
                        //ar.ReadUInt32Packed();
                        Logger.Error("TODO : Type not encoded");
                        // TypeHash = UOnlineEngineInterface::Get()->GetReplicationHashForSubsystem(UOnlineEngineInterface::Get()->GetDefaultOnlineSubsystemName());
                    }

                    FName type;
                    
                    var bValidTypeHash = typeHash != 0;
                    if (typeHash == TypeHashOther)
                    {
                        type = new FName(ar.ReadString());
                    }
                    else
                    {
                        type = new FName(EName.None);
                        // TODO: Type = UOnlineEngineInterface::Get()->GetSubsystemFromReplicationHash(TypeHash);
                    }

                    if (bValidTypeHash)
                    {
                        var contents = ar.ReadString();
                        if (!ar.IsError())
                        {
                            // TODO: Check if type != none
                            UniqueNetId = new FUniqueNetId(type, contents);
                            bOutSuccess = true;
                        }
                    }
                    else
                    {
                        Logger.Warning("Error with encoded type hash");
                    }
                }
            }
            else
            {
                Logger.Warning("Error serializing unique id");
            }
        }
    }
    
    private static void Serialize(FArchive ar, FUniqueNetIdRepl uniqueNetId)
    {
        if (!ar.IsPersistent() || ar._arIsNetArchive)
        {
            uniqueNetId.NetSerialize(ar, null, out _);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    private static byte GetTypeHashFromEncoding(EUniqueIdEncodingFlags inFlags)
    {
        var typeHash = (byte) ((byte) (inFlags & EUniqueIdEncodingFlags.TypeMask) >> 3);
        return (byte)(typeHash < 32 ? typeHash : 0);
    }
}
