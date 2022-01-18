using Prospect.Unreal.Core;
using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Serialization;

namespace Prospect.Unreal.Net;

public class FNetGUIDCache
{
	public UNetDriver? Driver { get; }
	public FNetGUIDCache(UNetDriver driver)
	{
		Driver = driver;
	}

	/*Dictionary<FNetworkGUID, FNetGuidCacheObject> ObjectLookup;
	Dictionary<UObject, FNetworkGUID> NetGUIDLookup;
	int[] UniqueNetIDs = new int[2];

	List<FNetworkGUID> ImportedNetGuids;
	Dictionary<FNetworkGUID, List<FNetworkGUID>> PendingOuterNetGuids;


	ENetworkChecksumMode NetworkChecksumMode;
	EAsyncLoadMode AsyncLoadMode;

	bool IsExportingNetGUIDBunch;

	Dictionary<FName, FNetworkGUID> PendingAsyncPackages;

	// Maps net field export group name to the respective FNetFieldExportGroup
	Dictionary<String, FNetFieldExportGroup> NetFieldExportGroupMap;

	// Maps field export group path to assigned index
	Dictionary<String, uint> NetFieldExportGroupPathToIndex;

	// Maps assigned net field export group index to pointer to group, lifetime of the referenced FNetFieldExportGroups are managed by NetFieldExportGroupMap
	Dictionary<uint, FNetFieldExportGroup> NetFieldExportGroupIndexToGroup;

	// Current index used when filling in NetFieldExportGroupPathToIndex/NetFieldExportGroupIndexToPath
	int UniqueNetFieldExportGroupPathIndex;

	// Store all GUIDs that caused the sync loading of a package, for debugging & logging with LogNetSyncLoads
	List<FNetworkGUID> SyncLoadedGUIDs;


	// Set of packages that are currently pending Async loads, referenced by package name.
	//Dictionary<FName, FPendingAsyncLoadRequest> PendingAsyncLoadRequests;
	//
	// Set of all current Objects that we've been requested to be referenced while channels
	// resolve their queued bunches. This is used to prevent objects (especially async load objects,
	// which may have no other references) from being GC'd while a channel is waiting for more
	// pending guids. 
	//Dictionary<FNetworkGUID, FQueuedBunchObjectReference> QueuedBunchObjectReferences;

	enum ENetworkChecksumMode : byte
	{
		None = 0,       // Don't use checksums
		SaveAndUse = 1,     // Save checksums in stream, and use to validate while loading packages
		SaveButIgnore = 2,      // Save checksums in stream, but ignore when loading packages
	};

	enum EAsyncLoadMode : byte
	{
		UseCVar = 0,        // Use CVar (net.AllowAsyncLoading) to determine if we should async load
		ForceDisable = 1,       // Disable async loading
		ForceEnable = 2,        // Force enable async loading
	};

	void CleanReferences()
	{

	}
	bool SupportsObject(UObject Object) { return false; }
	bool IsDynamicObject(UObject Object) { return false; }
	bool IsNetGUIDAuthority() { return false; }
	FNetworkGUID GetOrAssignNetGUID(UObject Object) { return new FNetworkGUID(0); }
	FNetworkGUID GetNetGUID(UObject Object) { return new FNetworkGUID(0); }
	FNetworkGUID GetOuterNetGUID(FNetworkGUID NetGUID) { return new FNetworkGUID(0); }
	FNetworkGUID AssignNewNetGUID_Server(UObject Object) { return new FNetworkGUID(0); }
	FNetworkGUID AssignNewNetGUIDFromPath_Server(String PathName, UObject ObjOuter, UClass ObjClass) { return new FNetworkGUID(0); }
	void RegisterNetGUID_Internal(FNetworkGUID NetGUID, FNetGuidCacheObject CacheObject ) { }
	void RegisterNetGUID_Server(FNetworkGUID NetGUID, UObject Object) { }
	void RegisterNetGUID_Client(FNetworkGUID NetGUID, UObject Object) { }
	void RegisterNetGUIDFromPath_Client(FNetworkGUID NetGUID, String PathName, FNetworkGUID OuterGUID, uint NetworkChecksum, bool bNoLoad, bool bIgnoreWhenMissing) { }
	void RegisterNetGUIDFromPath_Server(FNetworkGUID NetGUID, String PathName, FNetworkGUID OuterGUID, uint NetworkChecksum, bool bNoLoad, bool bIgnoreWhenMissing) { }
	UObject GetObjectFromNetGUID(FNetworkGUID NetGUID, bool bIgnoreMustBeMapped) { return new UObject(); }
	bool ShouldIgnoreWhenMissing(FNetworkGUID NetGUID) { return false; }
	//FNetGuidCacheObject GetCacheObject(FNetworkGUID NetGUID) { return null; }
	bool IsGUIDRegistered(FNetworkGUID NetGUID) { return false; }
	bool IsGUIDLoaded(FNetworkGUID NetGUID) { return false; }
	bool IsGUIDBroken(FNetworkGUID NetGUID, bool bMustBeRegistered) { return false; }
	bool IsGUIDNoLoad(FNetworkGUID NetGUID) { return false; }
	bool IsGUIDPending(FNetworkGUID NetGUID) { return false; }
	String FullNetGUIDPath(FNetworkGUID NetGUID) { return ""; }
	void GenerateFullNetGUIDPath_r(FNetworkGUID NetGUID, String FullPath) { }
	uint GetClassNetworkChecksum(UClass Class) { return 0; }
	uint GetNetworkChecksum(UObject Obj) { return 0; }
	void SetNetworkChecksumMode(ENetworkChecksumMode NewMode) { }
	void SetAsyncLoadMode(EAsyncLoadMode NewMode) { }
	bool ShouldAsyncLoad() { return false; }
	bool CanClientLoadObject(UObject Object, FNetworkGUID NetGUID) { return false; }

	void AsyncPackageCallback(FName PackageName, UPackage Package, EAsyncLoadingResult Result) { }

	void ResetCacheForDemo() { }

	void CountBytes(FArchive Ar) { }

	//	void ConsumeAsyncLoadDelinquencyAnalytics(FNetAsyncLoadDelinquencyAnalytics& Out) { }
	//const FNetAsyncLoadDelinquencyAnalytics GetAsyncLoadDelinquencyAnalytics() { }
	//void ResetAsyncLoadDelinquencyAnalytics() { }

	//void CollectReferences(class FReferenceCollector& ReferenceCollector) { }
	//List<FQueuedBunchObjectReference> TrackQueuedBunchObjectReference(FNetworkGUID InNetGUID, UObject InObject) { return null; }

	bool WasGUIDSyncLoaded(FNetworkGUID NetGUID) { return SyncLoadedGUIDs.Contains(NetGUID); }
	void ClearSyncLoadedGUID(FNetworkGUID NetGUID) { SyncLoadedGUIDs.Remove(NetGUID); }

	//If LogNetSyncLoads is enabled, log all objects that caused a sync load that haven't been otherwise reported by the package map yet, and clear that list.
	void ReportSyncLoadedGUIDs() { }

	//FNetAsyncLoadDelinquencyAnalytics DelinquentAsyncLoads;

	void StartAsyncLoadingPackage(FNetGuidCacheObject Object, FNetworkGUID ObjectGUID, bool bWasAlreadyAsyncLoading);
	void ValidateAsyncLoadingPackage(FNetGuidCacheObject Object, FNetworkGUID ObjectGUID);

	void UpdateQueuedBunchObjectReference(FNetworkGUID NetGUID, UObject NewObject);*/

}