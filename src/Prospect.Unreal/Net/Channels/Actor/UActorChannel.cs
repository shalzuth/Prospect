using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Core.Objects;
using Prospect.Unreal.Net.Packets.Bunch;
using Prospect.Unreal.Net.Packets.Control;
using Prospect.Unreal.Net.Actors;
using Prospect.Unreal.Serialization;

namespace Prospect.Unreal.Net.Channels.Actor;

public class UActorChannel : UChannel
{
    public AActor? Actor { get; set; }
    public FNetworkGUID? ActorNetGUID { get; set; }
	bool bSkipRoleSwap; // true if we should not swap the role and remote role of this actor when properties are received						
	bool bActorIsPendingKill; // Tracks whether or not our actor has been seen as pending kill.
	public UActorChannel()
	{
		ChType = EChannelType.CHTYPE_Actor;
        ChName = EName.Actor;
        // bClearRecentActorRefs = true;
        // bHoldQueuedExportBunchesAndGUIDs = false;
        // QueuedCloseReason = EChannelCloseReason::Destroyed;
    }

    public override void Tick()
    {
        base.Tick();
        // TODO: ProcessQueuedBunches
    }

    public override bool CanStopTicking()
    {
        return base.CanStopTicking() /* PendingGuidResolves / QueuedBunches */;
    }

    protected override void ReceivedBunch(FInBunch bunch)
    {
        if (Broken || bTornOff)
        {
            return;
        }
        if (Connection?.Driver?.IsServer() == true)
        {
            if (bunch.bHasMustBeMappedGUIDs)
            {
                bunch.SetError();
                return;
            }
        }
        else // is client
        {
            if (bunch.bHasMustBeMappedGUIDs)
            {
                var numMustBeMappedGUIDs = bunch.ReadUInt16();
                var mappedGuids = new List<FNetworkGUID>();
                for (int i = 0; i < numMustBeMappedGUIDs; i++)
                {
                    var guid = new FNetworkGUID(bunch.ReadUInt32Packed());
                    mappedGuids.Add(guid);
                    //Game.FNetGUIDCache[guid.Value] = guid;
                }
            }
        }

        // We can process this bunch now
        ProcessBunch(bunch);
    }
	public class FReplicationFlags
	{
		public bool bNetOwner;
		/** True if this is the initial network update for the replicating actor. */
		public bool bNetInitial;
		/** True if this is actor is RemoteRole simulated. */
		public bool bNetSimulated;
		/** True if this is actor's ReplicatedMovement.bRepPhysics flag is true. */
		public bool bRepPhysics;
		/** True if this actor is replicating on a replay connection. */
		public bool bReplay;
		/** True if this actor's RPCs should be ignored. */
		public bool bIgnoreRPCs;
		/** True if we should not swap the role and remote role of this actor when receiving properties. */
		public bool bSkipRoleSwap;
		/** True if we should only compare role properties in CompareProperties */
		public bool bRolesOnly;

		public uint Value;
	}
	enum ESetChannelActorFlags : uint
	{
		None = 0,
		SkipReplicatorCreation = (1 << 0),
		SkipMarkActive = (1 << 1),
	}
	void ProcessBunch(FInBunch Bunch )
	{
		if (Broken)
		{
			return;
		}

		FReplicationFlags RepFlags = new FReplicationFlags();

		// ------------------------------------------------------------
		// Initialize client if first time through.
		// ------------------------------------------------------------
		bool bSpawnedNewActor = false;  // If this turns to true, we know an actor was spawned (rather than found)
		if (Actor == null)
		{
			if (!Bunch.bOpen)
			{
				// This absolutely shouldn't happen anymore, since we no longer process packets until channel is fully open early on
				//"UActorChannel::ProcessBunch: New actor channel received non-open packet. bOpen: %i, bClose: %i, bReliable: %i, bPartial: %i, bPartialInitial: %i, bPartialFinal: %i, ChName: %s, ChIndex: %i, Closing: %i, OpenedLocally: %i, OpenAcked: %i, NetGUID: %s"), (int)Bunch.bOpen, (int)Bunch.bClose, (int)Bunch.bReliable, (int)Bunch.bPartial, (int)Bunch.bPartialInitial, (int)Bunch.bPartialFinal, *ChName.ToString(), ChIndex, (int)Closing, (int)OpenedLocally, (int)OpenAcked, *ActorNetGUID.ToString());
				return;
			}


			AActor? NewChannelActor = null;
			bSpawnedNewActor = ((UPackageMapClient)Connection.PackageMap).SerializeNewActor(Bunch, this, ref NewChannelActor);

			// We are unsynchronized. Instead of crashing, let's try to recover.
			if (NewChannelActor == null || NewChannelActor.IsPendingKill())
			{
				// got a redundant destruction info, possible when streaming
				if (!bSpawnedNewActor && Bunch.bReliable && Bunch.bClose && Bunch.AtEnd())
				{
					// Do not log during replay, since this is a valid case
					/*UDemoNetDriver* DemoNetDriver = Cast<UDemoNetDriver>(Connection->Driver);
					if (DemoNetDriver == nullptr)
					{
						UE_LOG(LogNet, Verbose, TEXT("UActorChannel::ProcessBunch: SerializeNewActor received close bunch for destroyed actor. Actor: %s, Channel: %i"), *GetFullNameSafe(NewChannelActor), ChIndex);
					}

					SetChannelActor(nullptr, ESetChannelActorFlags::None);
					return;*/
				}

				//check(!bSpawnedNewActor);
				//"UActorChannel::ProcessBunch: SerializeNewActor failed to find/spawn actor. Actor: %s, Channel: %i"), *GetFullNameSafe(NewChannelActor), ChIndex);
				Broken = true;

				if (!Connection.IsInternalAck())
				{
					NMT_ActorChannelFailure.Send(Connection, ChIndex);
				}
				return;
			}

			var Flags = ESetChannelActorFlags.None;
			var GSkipReplicatorForDestructionInfos = false;
			if (GSkipReplicatorForDestructionInfos && Bunch.bClose && Bunch.AtEnd())
			{
				Flags |= ESetChannelActorFlags.SkipReplicatorCreation;
			}

			//"      Channel Actor %s:"), *NewChannelActor->GetFullName());
			SetChannelActor(NewChannelActor, Flags);

			NotifyActorChannelOpen(Actor, Bunch);

			RepFlags.bNetInitial = true;

			//Actor.CustomTimeDilation = CustomTimeDilation;
		}
		else
		{
			//("      Actor %s:"), *Actor->GetFullName());
		}

		bool bLatestIsReplicationPaused = Bunch.bIsReplicationPaused;
		if (bLatestIsReplicationPaused != bIsReplicationPaused)
		{
			//Actor?.OnReplicationPausedChanged(bLatestIsReplicationPaused);
			//SetReplicationPaused(bLatestIsReplicationPaused);
		}

		// Owned by connection's player?
		var ActorConnection = Actor.GetNetConnection();
		if (ActorConnection == Connection || (ActorConnection != null && ActorConnection.IsA(UChildConnection.StaticClass()) && ((UChildConnection)ActorConnection)?.Parent == Connection))
		{
			RepFlags.bNetOwner = true;
		}

		RepFlags.bIgnoreRPCs = Bunch.bIgnoreRPCs;
		RepFlags.bSkipRoleSwap = bSkipRoleSwap;

		// ----------------------------------------------
		//	Read chunks of actor content
		// ----------------------------------------------
		while (!Bunch.AtEnd() && Connection != null && Connection.State != EConnectionState.USOCK_Closed)
		{
			//FNetBitReader Reader(Bunch.PackageMap, 0 );
			var Reader = new FNetBitReader(Bunch.PackageMap, null, 0);

			bool bHasRepLayout = false;

			// Read the content block header and payload
			var RepObj = ReadContentBlockPayload(Bunch, ref Reader, out bHasRepLayout);

			if (Bunch.IsError())
			{
				if (Connection.IsInternalAck())
				{
					//"UActorChannel::ReceivedBunch: ReadContentBlockPayload FAILED. Bunch.IsError() == TRUE. (IsInternalAck) Breaking actor. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
					Broken = true;
					break;
				}

				//"UActorChannel::ReceivedBunch: ReadContentBlockPayload FAILED. Bunch.IsError() == TRUE. Closing connection. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
				Connection.Close();
				return;
			}

			if (Reader.GetNumBits() == 0)
			{
				// Nothing else in this block, continue on (should have been a delete or create block)
				continue;
			}

			if (RepObj == null || RepObj.IsPendingKill())
			{
				if (Actor == null || Actor.IsPendingKill())
				{
					// If we couldn't find the actor, that's pretty bad, we need to stop processing on this channel
					//"UActorChannel::ProcessBunch: ReadContentBlockPayload failed to find/create ACTOR. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
					Broken = true;
				}
				else
				{
					//"UActorChannel::ProcessBunch: ReadContentBlockPayload failed to find/create object. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
				}

				continue;   // Since content blocks separate the payload from the main stream, we can skip to the next one
			}

			/*TSharedRef<FObjectReplicator> & Replicator = FindOrCreateReplicator(RepObj);

			bool bHasUnmapped = false;

			if (!Replicator->ReceivedBunch(Reader, RepFlags, bHasRepLayout, bHasUnmapped))
			{
				if (Connection.IsInternalAck())
				{
					//"UActorChannel::ProcessBunch: Replicator.ReceivedBunch failed (Ignoring because of IsInternalAck). RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
					Broken = true;
					continue;       // Don't consider this catastrophic in replays
				}

				// For now, with regular connections, consider this catastrophic, but someday we could consider supporting backwards compatibility here too
				//"UActorChannel::ProcessBunch: Replicator.ReceivedBunch failed.  Closing connection. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
				Connection.Close();
				return;
			}*/

			// Check to see if the actor was destroyed
			// If so, don't continue processing packets on this channel, or we'll trigger an error otherwise
			// note that this is a legitimate occurrence, particularly on client to server RPCs
			if (Actor == null || Actor.IsPendingKill())
			{
				//"UActorChannel::ProcessBunch: Actor was destroyed during Replicator.ReceivedBunch processing"));
				// If we lose the actor on this channel, we can no longer process bunches, so consider this channel broken
				Broken = true;
				break;
			}

			//if (bHasUnmapped)
			{
				//Connection?.Driver.UnmappedReplicators.Add(Replicator);
			}
		}

		/*
		for (auto RepComp = ReplicationMap.CreateIterator(); RepComp; ++RepComp)
		{
			TSharedRef<FObjectReplicator> & ObjectReplicator = RepComp.Value();
			if (ObjectReplicator->GetObject() == nullptr)
			{
				RepComp.RemoveCurrent();
				continue;
			}

			ObjectReplicator->PostReceivedBunch();
		}*/

		// After all properties have been initialized, call PostNetInit. This should call BeginPlay() so initialization can be done with proper starting values.
		if (Actor != null && bSpawnedNewActor)
		{
			Actor.PostNetInit();
		}
	}
	void SetChannelActor(AActor InActor, ESetChannelActorFlags Flags)
	{
		//check(!Closing);
		//check(Actor == nullptr);

		// Sanity check that the actor is in the same level collection as the channel's driver.
		var World = Connection?.Driver?.GetWorld();
		if (World != null && InActor != null)
		{
			/*var CachedLevel = InActor->GetLevel();
			var ActorCollection = CachedLevel->GetCachedLevelCollection();
			if (ActorCollection && ActorCollection->GetNetDriver() != Connection.Driver && ActorCollection->GetDemoNetDriver() != Connection.Driver)
			{
				//"UActorChannel::SetChannelActor: actor %s is not in the same level collection as the net driver (%s)!"), *GetFullNameSafe(InActor), *GetFullNameSafe(Connection->Driver));
			}*/
		}

		// Set stuff.
		Actor = InActor;

		// We could check Actor->IsPendingKill here, but that would supress the warning later.
		// Further, expect calling code to do these checks.
		bActorIsPendingKill = false;

		//"SetChannelActor: ChIndex: %i, Actor: %s, NetGUID: %s"), ChIndex, Actor ? *Actor->GetFullName() : TEXT("NULL"), *ActorNetGUID.ToString());

		if (ChIndex >= 0 && Connection?.PendingOutRec[ChIndex] > 0)
		{
			// send empty reliable bunches to synchronize both sides
			// UE_LOG(LogNetTraffic, Log, TEXT("%i Actor %s WILL BE sending %i vs first %i"), ChIndex, *Actor->GetName(), Connection->PendingOutRec[ChIndex],Connection->OutReliable[ChIndex]);
			var RealOutReliable = Connection.OutReliable[ChIndex];
			Connection.OutReliable[ChIndex] = Connection.PendingOutRec[ChIndex] - 1;
			while (Connection.PendingOutRec[ChIndex] <= RealOutReliable)
			{
				// UE_LOG(LogNetTraffic, Log, TEXT("%i SYNCHRONIZING by sending %i"), ChIndex, Connection->PendingOutRec[ChIndex]);

				var Bunch = new FOutBunch(this, false);

				if (!Bunch.IsError())
				{
					Bunch.bReliable = true;
					SendBunch(Bunch, false);
					Connection.PendingOutRec[ChIndex]++;
				}
				else
				{
					// While loop will be infinite without either fatal or break.
					// UE_LOG(LogNetTraffic, Fatal, TEXT("SetChannelActor failed. Overflow while sending reliable bunch synchronization."));
					break;
				}
			}

			Connection.OutReliable[ChIndex] = RealOutReliable;
			Connection.PendingOutRec[ChIndex] = 0;
		}

		if (Actor != null)
		{
			// Add to map.
			Connection?.AddActorChannel(Actor, this);

			//check(!ReplicationMap.Contains(Actor));

			// Create the actor replicator, and store a quick access pointer to it
			if (!Flags.HasFlag(ESetChannelActorFlags.SkipReplicatorCreation))
			{
				//ActorReplicator = FindOrCreateReplicator(Actor);
			}

			if (!Flags.HasFlag(ESetChannelActorFlags.SkipMarkActive))
			{
				// Remove from connection's dormancy lists
				//Connection.Driver.GetNetworkObjectList().MarkActive(Actor, Connection, Connection.Driver);
				//Connection.Driver.GetNetworkObjectList().ClearRecentlyDormantConnection(Actor, Connection, Connection.Driver);
			}
		}
	}
	void NotifyActorChannelOpen(AActor InActor, FInBunch InBunch)
	{
		var NetDriver = Connection?.Driver;
		var World = NetDriver?.World;
		/*var Context = GEngine?.GetWorldContextFromWorld(World);
		if (Context != null)
		{
			foreach(var Driver in Context.ActiveNetDrivers)
			{
				if (Driver.NetDriver != null)
				{
					Driver.NetDriver->NotifyActorChannelOpen(this, InActor);
				}
			}
		}*/

		Actor?.OnActorChannelOpen(InBunch, Connection);

		if (NetDriver != null && !NetDriver.IsServer())
		{
			if (Actor?.NetDormancy > AActor.ENetDormancy.DORM_Awake)
			{
				Actor.NetDormancy = AActor.ENetDormancy.DORM_Awake;

				/*var DemoNetDriver = World.GetDemoNetDriver();

				// if recording on client, make sure the actor is marked active
				if (World && World.IsRecordingClientReplay() && DemoNetDriver)
				{
					DemoNetDriver.GetNetworkObjectList().FindOrAdd(Actor, DemoNetDriver);
					DemoNetDriver.FlushActorDormancy(Actor);

					var  DemoClientConnection = (DemoNetDriver->ClientConnections.Num() > 0) ? DemoNetDriver->ClientConnections[0] : nullptr;
					if (DemoClientConnection)
					{
						DemoNetDriver->GetNetworkObjectList().MarkActive(Actor, DemoClientConnection, DemoNetDriver);
						DemoNetDriver->GetNetworkObjectList().ClearRecentlyDormantConnection(Actor, DemoClientConnection, DemoNetDriver);
					}
				}*/
			}
		}
	}
	UObject? ReadContentBlockPayload(FInBunch Bunch, ref FNetBitReader OutPayload, out bool bOutHasRepLayout )
	{
		var StartHeaderBits = Bunch.GetPosBits();
		bool bObjectDeleted = false;
		UObject RepObj = ReadContentBlockHeader(Bunch, bObjectDeleted, out bOutHasRepLayout);

		if (Bunch.IsError())
		{
			//UE_LOG(LogNet, Error, TEXT("UActorChannel::ReadContentBlockPayload: ReadContentBlockHeader FAILED. Bunch.IsError() == TRUE. Closing connection. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
			return null;
		}

		if (bObjectDeleted)
		{
			OutPayload.SetData(Bunch, 0);

			// Nothing else in this block, continue on
			return null;
		}

		var NumPayloadBits = 0u;
		Bunch.SerializeIntPacked(NumPayloadBits);

		if (Bunch.IsError())
		{
			//UE_LOG(LogNet, Error, TEXT("UActorChannel::ReceivedBunch: Read NumPayloadBits FAILED. Bunch.IsError() == TRUE. Closing connection. RepObj: %s, Channel: %i"), RepObj ? *RepObj->GetFullName() : TEXT("NULL"), ChIndex);
			return null;
		}

		OutPayload.SetData(Bunch, NumPayloadBits);

		return RepObj;
	}
	UObject? ReadContentBlockHeader(FInBunch Bunch, bool bObjectDeleted, out bool bOutHasRepLayout )
	{
		var IsServer = Connection.Driver.IsServer();
		bObjectDeleted = false;

		bOutHasRepLayout = Bunch.ReadBit();

		if (Bunch.IsError())
		{
			//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Bunch.IsError() == true after bOutHasRepLayout. Actor: %s"), *Actor->GetName());
			return null;
		}

		var bIsActor = Bunch.ReadBit();

		if (Bunch.IsError())
		{
			//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Bunch.IsError() == true after reading actor bit. Actor: %s"), *Actor->GetName());
			return null;
		}

		if (bIsActor)
		{
			// If this is for the actor on the channel, we don't need to read anything else
			return Actor;
		}

		//
		// We need to handle a sub-object
		//

		// Note this heavily mirrors what happens in UPackageMapClient::SerializeNewActor
		UObject SubObj = null;

		// Manually serialize the object so that we can get the NetGUID (in order to assign it if we spawn the object here)
		((UPackageMapClient)Connection.PackageMap)?.SerializeObject(Bunch, UObject.StaticClass(), ref SubObj, out FNetworkGUID NetGUID);


		if (Bunch.IsError())
		{
			//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Bunch.IsError() == true after SerializeObject. SubObj: %s, Actor: %s"), SubObj ? *SubObj->GetName() : TEXT("Null"), *Actor->GetName());
			Bunch.SetError();
			return null;
		}

		if (Bunch.AtEnd())
		{
			//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Bunch.AtEnd() == true after SerializeObject. SubObj: %s, Actor: %s"), SubObj ? *SubObj->GetName() : TEXT("Null"), *Actor->GetName());
			Bunch.SetError();
			return null;
		}

		// Validate existing sub-object
		if (SubObj != null)
		{
			// Sub-objects can't be actors (should just use an actor channel in this case)
			if (SubObj != null && SubObj is AActor)
			{
				//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Sub-object not allowed to be actor type. SubObj: %s, Actor: %s"), *SubObj->GetName(), *Actor->GetName());
				Bunch.SetError();
				return null;
			}

			// Sub-objects must reside within their actor parents
			/*if (!SubObj.IsIn(Actor))
			{
				//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Sub-object not in parent actor. SubObj: %s, Actor: %s"), *SubObj->GetFullName(), *Actor->GetFullName());

				if (IsServer)
				{
					Bunch.SetError();
					return null;
				}
			}*/
		}

		if (IsServer)
		{
			// The server should never need to create sub objects
			if (SubObj == null)
			{
				//UE_LOG(LogNetTraffic, Error, TEXT("ReadContentBlockHeader: Client attempted to create sub-object. Actor: %s"), *Actor->GetName());
				Bunch.SetError();
				return null;
			}

			return SubObj;
		}

		var bStablyNamed = Bunch.ReadBit();

		if (Bunch.IsError())
		{
			//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Bunch.IsError() == true after reading stably named bit. Actor: %s"), *Actor->GetName());
			return null;
		}

		if (bStablyNamed)
		{
			// If this is a stably named sub-object, we shouldn't need to create it. Don't raise a bunch error though because this may happen while a level is streaming out.
			if (SubObj == null)
			{
				// (ignore though if this is for replays)
				if (!Connection.IsInternalAck())
				{
					//UE_LOG(LogNetTraffic, Log, TEXT("ReadContentBlockHeader: Stably named sub-object not found. Its level may have streamed out. Component: %s, Actor: %s"), *Connection->Driver->GuidCache->FullNetGUIDPath(NetGUID), *Actor->GetName());
				}

				return null;
			}

			return SubObj;
		}

		// Serialize the class in case we have to spawn it.
		// Manually serialize the object so that we can get the NetGUID (in order to assign it if we spawn the object here)
		UObject SubObjClassObj = null;
		((UPackageMapClient)Connection.PackageMap).SerializeObject(Bunch, UObject.StaticClass(), ref SubObjClassObj, out FNetworkGUID ClassNetGUID);

		// Delete sub-object
		if (!ClassNetGUID.IsValid())
		{
			if (SubObj != null)
			{
				// Unmap this object so we can remap it if it becomes relevant again in the future
				//MoveMappedObjectToUnmapped(SubObj);

				// Stop tracking this sub-object
				//CreateSubObjects.Remove(SubObj);

				if (Connection != null && Connection.Driver != null)
				{
					//Connection?.Driver?.RepChangedPropertyTrackerMap.Remove(SubObj);
				}

				//Actor.OnSubobjectDestroyFromReplication(SubObj);

				//SubObj.PreDestroyFromReplication();
				//SubObj.MarkPendingKill();
			}
			bObjectDeleted = true;
			return null;
		}

		var SubObjClass = (UClass)SubObjClassObj;

		if (SubObjClass == null)
		{
			//UE_LOG(LogNetTraffic, Warning, TEXT("UActorChannel::ReadContentBlockHeader: Unable to read sub-object class. Actor: %s"), *Actor->GetName());

			// Valid NetGUID but no class was resolved - this is an error
			if (SubObj == null)
			{
				// (unless we're using replays, which could be backwards compatibility kicking in)
				if (!Connection.IsInternalAck())
				{
					//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Unable to read sub-object class (SubObj == NULL). Actor: %s"), *Actor->GetName());
					Bunch.SetError();
				}

				return null;
			}
		}
		else
		{
			if (SubObjClass == UObject.StaticClass())
			{
				//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: SubObjClass == UObject::StaticClass(). Actor: %s"), *Actor->GetName());
				Bunch.SetError();
				return null;
			}

			if (SubObjClass.IsChildOf(AActor.StaticClass()))
			{
				//UE_LOG(LogNetTraffic, Error, TEXT("UActorChannel::ReadContentBlockHeader: Sub-object cannot be actor class. Actor: %s"), *Actor->GetName());
				Bunch.SetError();
				return null;
			}
		}

		if (SubObj == null)
		{
			/*check(!IsServer);

			// Construct the sub-object
			UE_LOG(LogNetTraffic, Log, TEXT("UActorChannel::ReadContentBlockHeader: Instantiating sub-object. Class: %s, Actor: %s"), *SubObjClass->GetName(), *Actor->GetName());

			SubObj = NewObject<UObject>(Actor, SubObjClass);

			// Sanity check some things
			check(SubObj != NULL);
			check(SubObj->IsIn(Actor));
			check(Cast<AActor>(SubObj) == NULL);

			// Notify actor that we created a component from replication
			Actor->OnSubobjectCreatedFromReplication(SubObj);

			// Register the component guid
			Connection->Driver->GuidCache->RegisterNetGUID_Client(NetGUID, SubObj);

			// Track which sub-object guids we are creating
			CreateSubObjects.Add(SubObj);

			// Add this sub-object to the ImportedNetGuids list so we can possibly map this object if needed
			if (ensureMsgf(NetGUID.IsValid(), TEXT("Channel tried to add an invalid GUID to the import list: %s"), *Describe()))
			{
				Connection->Driver->GuidCache->ImportedNetGuids.Add(NetGUID);
			}*/
		}

		return SubObj;
	}
}