using Content.Shared.Pinpointer;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Shared.SurveillanceCamera;

public abstract class SharedSurveillanceCameraMapSystem : EntitySystem
{
    [Dependency] private readonly SharedNavMapSystem _navMap = default!;


    public override void Initialize()
    {
        _navMap.SubscribeUiToNavMapWarpRequest<SurveillanceCameraComponent>(Subs,
            SurveillanceCameraMonitorUiKey.Key);
    }
}
