using Content.Server.Medical.CrewMonitoring;
using Content.Shared.Pinpointer;

namespace Content.Shared.Medical.CrewMonitoring;

public abstract class SharedCrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedNavMapSystem _navMap = default!;

    public override void Initialize()
    {
        _navMap.SubscribeUiToNavMapWarpRequest<CrewMonitoringConsoleComponent>(Subs, CrewMonitoringUIKey.Key);
    }
}
