using Prospect.Unreal.Core;
using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Core.Objects;
using Prospect.Unreal.Net;
using Prospect.Unreal.Net.Actors;
using Prospect.Unreal.Net.Channels.Actor;

namespace Prospect.Unreal.Runtime;

public class UReplicationConnectionDriver : UObject
{
	public virtual void NotifyActorChannelAdded(AActor Actor, UActorChannel Channel) { }

	public virtual void NotifyActorChannelRemoved(AActor Actor) { }

	public virtual void NotifyActorChannelCleanedUp(UActorChannel Channel) { }

	//public virtual void NotifyAddDestructionInfo(FActorDestructionInfo* DestructInfo);

	public virtual void NotifyAddDormantDestructionInfo(AActor Actor) { }

	//public virtual void NotifyRemoveDestructionInfo(FActorDestructionInfo* DestructInfo);

	public virtual void NotifyResetDestructionInfo() { }

	public virtual void NotifyClientVisibleLevelNamesAdd(FName LevelName, UWorld StreamingWorld) { }

	public virtual void NotifyClientVisibleLevelNamesRemove(FName LevelName) { }

	//public virtual void TearDown() { MarkPendingKill(); }
}