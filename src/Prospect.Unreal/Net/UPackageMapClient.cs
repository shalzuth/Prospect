using Prospect.Unreal.Core;
using Prospect.Unreal.Core.Math;
using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Core.Objects;
using Prospect.Unreal.Exceptions;
using Prospect.Unreal.Net.Packets.Bunch;
using Prospect.Unreal.Serialization;
using Prospect.Unreal.Net.Actors;
using Prospect.Unreal.Net.Channels.Actor;
using Prospect.Unreal.Runtime;

namespace Prospect.Unreal.Net;

public class UPackageMapClient : UPackageMap
{
	readonly int MaxGUIDCount = 2048;
	readonly int InternalLoadObjectRecursionLimit = 16;
	UNetConnection? Connection { get; set; }
	public void Initialize(UNetConnection connection, FNetGUIDCache guidCache)
	{
		Connection = connection;
		// TODO: Implement
	}

	public void ReceiveNetGUIDBunch(FInBunch InBunch)
	{
		//check(InBunch.bHasPackageMapExports);
		if (!InBunch.bHasPackageMapExports)
		{
			throw new UnrealException("Not exporting map");
		}

		var StartingBitPos = InBunch.GetPosBits();
		var bHasRepLayoutExport = InBunch.ReadBit();

		if (bHasRepLayoutExport)
		{
			// We need to keep this around to ensure we don't break backwards compatability.
			//ReceiveNetFieldExportsCompat(InBunch);
			return;
		}

		var NumGUIDsInBunch = InBunch.ReadInt32();

		if (NumGUIDsInBunch > MaxGUIDCount)
		{
			//UE_LOG(LogNetPackageMap, Error, TEXT("UPackageMapClient::ReceiveNetGUIDBunch: NumGUIDsInBunch > MAX_GUID_COUNT (%i)"), NumGUIDsInBunch);
			InBunch.SetError();
			return;
		}

		//UE_LOG(LogNetPackageMap, Log, TEXT("UPackageMapClient::ReceiveNetGUIDBunch %d NetGUIDs. PacketId %d. ChSequence %d. ChIndex %d"), NumGUIDsInBunch, InBunch.PacketId, InBunch.ChSequence, InBunch.ChIndex);

		var NumGUIDsRead = 0;
		while (NumGUIDsRead < NumGUIDsInBunch)
		{
			var LoadedGUID = InBunch.ReadUInt32Packed();// InternalLoadObject(InBunch, out UObject Obj, 0);

			if (InBunch.IsError())
			{
				//UE_LOG(LogNetPackageMap, Error, TEXT("UPackageMapClient::ReceiveNetGUIDBunch: InBunch.IsError() after InternalLoadObject"));
				return;
			}
			NumGUIDsRead++;
		}

		//UE_LOG(LogNetPackageMap, Log, TEXT("UPackageMapClient::ReceiveNetGUIDBunch end. BitPos: %d"), InBunch.GetPosBits());
	}

	//This is the meat of the PackageMap class which serializes a reference to Object.
	public bool SerializeObject(FArchive Ar, UClass Class, ref UObject? Object, out FNetworkGUID NetGUID)
	{
		NetGUID = new FNetworkGUID(0);
		if (Ar.IsSaving())
		{
			// If pending kill, just serialize as NULL.
			// TWeakObjectPtrs of PendingKill objects will behave strangely with TSets and TMaps
			//	PendingKill objects will collide with each other and with NULL objects in those data structures.
			if (Object != null && Object.IsPendingKill())
			{
				Object = null;
				return SerializeObject(Ar, Class, ref Object, out NetGUID);
			}

			/*NetGUID = GuidCache->GetOrAssignNetGUID(Object);

			// Write object NetGUID to the given FArchive
			InternalWriteObject(Ar, NetGUID, Object, "", null);

			// If we need to export this GUID (its new or hasnt been ACKd, do so here)
			if (!NetGUID.IsDefault() && ShouldSendFullPath(Object, NetGUID))
			{
				check(IsNetGUIDAuthority());
				if (!ExportNetGUID(NetGUID, Object, TEXT(""), NULL))
				{
					UE_LOG(LogNetPackageMap, Verbose, TEXT("Failed to export in ::SerializeObject %s"), *Object->GetName());
				}
			}*/

			return true;
		}
		else if (Ar.IsLoading())
		{
			double LoadTime = 0.0;
			// ----------------	
			// Read NetGUID from stream and resolve object
			// ----------------	
			/*NetGUID = InternalLoadObject(Ar, Object, 0);

			// ----------------	
			// Final Checks/verification
			// ----------------	

			// NULL if we haven't finished loading the objects level yet
			if (!ObjectLevelHasFinishedLoading(Object))
			{
				//"Using None instead of replicated reference to %s because the level it's in has not been made visible"), *Object->GetFullName());
				Object = null;
			}

			// Check that we got the right class
			if (Object && !Object->IsA(Class))
			{
				//"Forged object: got %s, expecting %s"), *Object->GetFullName(), *Class->GetFullName());
				Object = null;
			}

			if (NetGUID.IsValid() && bShouldTrackUnmappedGuids && !GuidCache->IsGUIDBroken(NetGUID, false))
			{
				if (Object == null)
				{
					TrackedUnmappedNetGuids.Add(NetGUID);
				}
				else if (NetGUID.IsDynamic())
				{
					TrackedMappedDynamicNetGuids.Add(NetGUID);
				}
			}

			if (bNetReportSyncLoads && NetGUID.IsValid())
			{
				// Track the GUID of anything in the outer chain that was sync loaded, to catch packages.
				for (FNetworkGUID CurrentGUID = NetGUID; CurrentGUID.IsValid(); CurrentGUID = GuidCache->GetOuterNetGUID(CurrentGUID))
				{
					if (GuidCache->WasGUIDSyncLoaded(CurrentGUID))
					{
						TrackedSyncLoadedGUIDs.Add(CurrentGUID);
					}
				}
			}*/

			//"UPackageMapClient::SerializeObject Serialized Object %s as <%s>"), Object ? *Object->GetPathName() : TEXT("NULL"), *NetGUID.ToString());
		}
		// reference is mapped if it was not NULL (or was explicitly null)
		return (Object != null);// || !NetGUID.IsValid());
	}

	public bool SerializeNewActor(FArchive Ar, UActorChannel Channel, ref AActor Actor)
	{
		//UE_LOG(LogNetPackageMap, VeryVerbose, TEXT( "SerializeNewActor START" ) );

		bool bIsClosingChannel = false;

		if (Ar.IsLoading())
		{
			bIsClosingChannel = ((FInBunch)Ar).bClose;      // This is so we can determine that this channel was opened/closed for destruction
															//UE_LOG(LogNetPackageMap, Log, TEXT("UPackageMapClient::SerializeNewActor BitPos: %d"), InBunch->GetPosBits() );
															//ResetTrackedSyncLoadedGuids();
		}
		UObject? obj = (UObject)Actor;
		SerializeObject(Ar, AActor.StaticClass(), ref obj, out FNetworkGUID NetGUID);
		Actor = (AActor)obj;
		if (Ar.IsError())
		{
			//"UPackageMapClient::SerializeNewActor: Ar.IsError after SerializeObject 1"));
			return false;
		}

		/*bool bFilterGuidRemapping = (CVarFilterGuidRemapping.GetValueOnAnyThread() > 0);
	if (!bFilterGuidRemapping)
	{
		if (GuidCache.IsValid())
		{
			if (ensureMsgf(NetGUID.IsValid(), TEXT("Channel tried to add an invalid GUID to the import list: %s"), *Channel->Describe()))
			{
				GuidCache->ImportedNetGuids.Add(NetGUID);
			}
		}
	}*/

		Channel.ActorNetGUID = NetGUID;


		// When we return an actor, we don't necessarily always spawn it (we might have found it already in memory)
		// The calling code may want to know, so this is why we distinguish
		bool bActorWasSpawned = false;

		if (Ar.AtEnd() && NetGUID.IsDynamic())
		{
			// This must be a destruction info coming through or something is wrong
			// If so, we should be closing the channel
			// This can happen when dormant actors that don't have channels get destroyed
			// Not finding the actor can happen if the client streamed in this level after a dynamic actor has been spawned and deleted on the server side
			if (!bIsClosingChannel)
			{
				//"UPackageMapClient::SerializeNewActor: bIsClosingChannel == 0 : %s [%s]"), *GetNameSafe(Actor), *NetGUID.ToString());
				Ar.SetError();
				return false;
			}

			//"UPackageMapClient::SerializeNewActor:  Skipping full read because we are deleting dynamic actor: %s"), *GetNameSafe(Actor));
			return false;       // This doesn't mean an error. This just simply means we didn't spawn an actor.
		}

		/*
		if (bFilterGuidRemapping)
		{
			// Do not mark guid as imported until we know we aren't deleting it
			if (GuidCache.IsValid())
			{
				if (ensureMsgf(NetGUID.IsValid(), TEXT("Channel tried to add an invalid GUID to the import list: %s"), *Channel->Describe()))
				{
					GuidCache->ImportedNetGuids.Add(NetGUID);
				}
			}
		}*/

		if (NetGUID.IsDynamic())
		{
			UObject? Archetype = null;
			UObject? ActorLevel = null;
			FVector Location = new FVector();
			FVector Scale = new FVector();
			FVector Velocity = new FVector();
			FRotator Rotation = new FRotator();
			bool SerSuccess;

			if (Ar.IsSaving())
			{
				// ChildActor's need to be spawned from the ChildActorTemplate otherwise any non-replicated 
				// customized properties will be incorrect on the Client.
				if (Actor.GetParentComponent() != null)
				{
					Archetype = Actor.GetParentComponent()?.GetChildActorTemplate();
				}
				if (Archetype == null)
				{
					Archetype = Actor.GetArchetype();
				}
				ActorLevel = Actor.GetLevel();

				//check(Archetype != nullptr);
				//check(Actor->NeedsLoadForClient());         // We have no business sending this unless the client can load
				//check(Archetype->NeedsLoadForClient());     // We have no business sending this unless the client can load

				var RootComponent = Actor.GetRootComponent();

				if (RootComponent != null)
				{
					//Location = FRepMovement.RebaseOntoZeroOrigin(Actor.GetActorLocation(), Actor);
					Location = Actor.GetActorLocation();
				}
				else
				{
					Location = FVector.ZeroVector;
				}
				Rotation = RootComponent != null ? Actor.GetActorRotation() : FRotator.ZeroRotator;
				Scale = RootComponent != null ? Actor.GetActorScale() : FVector.OneVector;
				Velocity = RootComponent != null ? Actor.GetVelocity() : FVector.ZeroVector;
			}

			SerializeObject(Ar, UObject.StaticClass(), ref Archetype, out FNetworkGUID ArchetypeNetGUID);

			if (Ar.IsSaving() || (Connection != null && (Connection.EngineNetworkProtocolVersion >= EEngineNetworkVersionHistory.HISTORY_NEW_ACTOR_OVERRIDE_LEVEL)))
			{
				SerializeObject(Ar, ULevel.StaticClass(), ref ActorLevel, out _);
			}

			if (ArchetypeNetGUID.IsValid() && Archetype == null)
			{
				/*var ExistingCacheObjectPtr = GuidCache->ObjectLookup.Find(ArchetypeNetGUID);

				if (ExistingCacheObjectPtr != null)
				{
					//"UPackageMapClient::SerializeNewActor. Unresolved Archetype GUID. Path: %s, NetGUID: %s."), *ExistingCacheObjectPtr->PathName.ToString(), *ArchetypeNetGUID.ToString());
				}
				else
				{
					//"UPackageMapClient::SerializeNewActor. Unresolved Archetype GUID. Guid not registered! NetGUID: %s."), *ArchetypeNetGUID.ToString());
				}*/
			}

			// SerializeCompressedInitial
			// only serialize the components that need to be serialized otherwise default them
			bool bSerializeLocation = false;
			bool bSerializeRotation = false;
			bool bSerializeScale = false;
			bool bSerializeVelocity = false;

			{
				// Server is serializing an object to be sent to a client
				if (Ar.IsSaving())
				{
					// We use 0.01f for comparing when using quantization, because we will only send a single point of precision anyway.
					// We could probably get away with 0.1f, but that may introduce edge cases for rounding.
					float Epsilon_Quantized = 0.01f;

					// We use KINDA_SMALL_NUMBER for comparing when not using quantization, because that's the default for FVector::Equals.
					float Epsilon = 0.0001f;

					//bSerializeLocation = !Location.Equals(FVector::ZeroVector, GbQuantizeActorLocationOnSpawn ? Epsilon_Quantized : Epsilon);
					//bSerializeVelocity = !Velocity.Equals(FVector::ZeroVector, GbQuantizeActorVelocityOnSpawn ? Epsilon_Quantized : Epsilon);
					//bSerializeScale = !Scale.Equals(FVector::OneVector, GbQuantizeActorScaleOnSpawn ? Epsilon_Quantized : Epsilon);

					// We use 0.001f for Rotation comparison to keep consistency with old behavior.
					//bSerializeRotation = !Rotation.IsNearlyZero(0.001f);

				}

				Func<FVector, FVector, bool, bool, bool> ConditionallySerializeQuantizedVector = new Func<FVector, FVector, bool, bool, bool>((FVector InOutValue, FVector DefaultValue, bool bShouldQuantize, bool bWasSerialized) =>
				{
					Ar.SerializeBits(new byte[1] { (byte)(bWasSerialized ? 1 : 0) }, 1);
					if (bWasSerialized)
					{
						if (Ar.EngineNetVer() < EEngineNetworkVersionHistory.HISTORY_OPTIONALLY_QUANTIZE_SPAWN_INFO)
						{
							bShouldQuantize = true;
						}
						else
						{
							Ar.SerializeBits(new byte[1] { (byte)(bShouldQuantize ? 1 : 0) }, 1);
						}

						if (bShouldQuantize)
						{
							FVector_NetQuantize10 Temp = (FVector_NetQuantize10)InOutValue;
							//Temp.NetSerialize(Ar, this, out SerSuccess);
							InOutValue = Temp;
						}
						else
						{
							//Ar.SerializeBits(InOutValue.ToArray(), ?);
						}
					}
					else
					{
						InOutValue = DefaultValue;
					}
					return false;
				});
				var GbQuantizeActorLocationOnSpawn = false;
				var GbQuantizeActorScaleOnSpawn = false;
				var GbQuantizeActorVelocityOnSpawn = false;
				ConditionallySerializeQuantizedVector(Location, FVector.ZeroVector, GbQuantizeActorLocationOnSpawn, bSerializeLocation);

				Ar.SerializeBits(new byte[1] { (byte)(bSerializeRotation ? 1 : 0) }, 1);
				if (bSerializeRotation)
				{
					//Rotation.NetSerialize(Ar, this, out SerSuccess);
				}
				else
				{
					Rotation = FRotator.ZeroRotator;
				}

				ConditionallySerializeQuantizedVector(Scale, FVector.OneVector, GbQuantizeActorScaleOnSpawn, bSerializeScale);
				ConditionallySerializeQuantizedVector(Velocity, FVector.ZeroVector, GbQuantizeActorVelocityOnSpawn, bSerializeVelocity);
			}

			if (Ar.IsLoading())
			{
				// Spawn actor if necessary (we may have already found it if it was dormant)
				if (Actor == null)
				{
					if (Archetype != null)
					{
						// For streaming levels, it's possible that the owning level has been made not-visible but is still loaded.
						// In that case, the level will still be found but the owning world will be invalid.
						// If that happens, wait to spawn the Actor until the next time the level is streamed in.
						// At that point, the Server should resend any dynamic Actors.
						var SpawnLevel = (ULevel)ActorLevel;
						if (SpawnLevel == null || SpawnLevel.GetWorld() != null)
						{
							FActorSpawnParameters SpawnInfo = new FActorSpawnParameters();
							SpawnInfo.Template = (AActor)(Archetype);
							SpawnInfo.OverrideLevel = SpawnLevel;
							SpawnInfo.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod.AlwaysSpawn;
							SpawnInfo.bRemoteOwned = true;
							SpawnInfo.bNoFail = true;

							var World = Connection.Driver.GetWorld();
							FVector SpawnLocation = Location;// FRepMovement.RebaseOntoLocalOrigin(Location, World.OriginLocation);
							Actor = World.SpawnActor(Archetype.GetClass(), new FTransform { Rotation = Rotation, Location = SpawnLocation }, SpawnInfo);
							if (Actor != null)
							{
								// Velocity was serialized by the server
								if (bSerializeVelocity)
								{
									Actor.PostNetReceiveVelocity(Velocity);
								}

								// Scale was serialized by the server
								if (bSerializeScale)
								{
									Actor.SetActorScale3D(Scale);
								}

								//GuidCache.RegisterNetGUID_Client(NetGUID, Actor);
								bActorWasSpawned = true;
							}
							else
							{
								//"SerializeNewActor: Failed to spawn actor for NetGUID: %s, Channel: %d"), *NetGUID.ToString(), Channel->ChIndex);
							}
						}
						else
						{
							//"SerializeNewActor: Actor level has invalid world (may be streamed out). NetGUID: %s, Channel: %d"), *NetGUID.ToString(), Channel->ChIndex);
						}
					}
					else
					{
						//"UPackageMapClient::SerializeNewActor Unable to read Archetype for NetGUID %s / %s"), *NetGUID.ToString(), *ArchetypeNetGUID.ToString());
					}
				}
			}
		}
		else if (Ar.IsLoading() && Actor == null)
		{
			// Do not log a warning during replay, since this is a valid case
			/*UDemoNetDriver* DemoNetDriver = Cast<UDemoNetDriver>(Connection->Driver);
			if (DemoNetDriver == nullptr)
			{
				//"SerializeNewActor: Failed to find static actor: FullNetGuidPath: %s, Channel: %d"), *GuidCache->FullNetGUIDPath(NetGUID), Channel->ChIndex);
			}
			/*
			if (bFilterGuidRemapping)
			{
				// Do not attempt to resolve this missing actor
				if (GuidCache.IsValid())
				{
					GuidCache->ImportedNetGuids.Remove(NetGUID);
				}
			}*/
		}

		if (Ar.IsLoading())
		{
			//ReportSyncLoadsForActorSpawn(Actor);
		}

		//"SerializeNewActor END: Finished Serializing. Actor: %s, FullNetGUIDPath: %s, Channel: %d, IsLoading: %i, IsDynamic: %i"), Actor ? *Actor->GetName() : TEXT("NULL"), *GuidCache->FullNetGUIDPath(NetGUID), Channel->ChIndex, (int)Ar.IsLoading(), (int)NetGUID.IsDynamic());

		return bActorWasSpawned;
	}
}