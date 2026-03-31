using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

public abstract class SharedStationMapSystem : EntitySystem
{
    [Dependency] private readonly SharedNavMapSystem _navMap = default!;

    public override void Initialize()
    {
        _navMap.SubscribeUiToNavMapWarpRequest<StationMapComponent>(Subs, StationMapUiKey.Key);
    }
}

[Serializable, NetSerializable]
public enum StationMapUiKey : byte
{
    Key,
}
