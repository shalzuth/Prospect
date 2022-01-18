namespace Prospect.Unreal.Core.Objects;

public class UObject : UObjectBaseUtility
{
    public bool IsPendingKill() { return false; }
    public UObject GetArchetype() { return null; }
}