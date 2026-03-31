using Content.Shared.Atmos.Components;
using Content.Shared.Pinpointer;

namespace Content.Shared.Atmos.Consoles;

public abstract partial class SharedAtmosAlertsComputerSystem : EntitySystem
{
    [Dependency] private readonly SharedNavMapSystem _navMap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AtmosAlertsComputerComponent, AtmosAlertsComputerDeviceSilencedMessage>(OnDeviceSilencedMessage);
        _navMap.SubscribeUiToNavMapWarpRequest<AtmosAlertsComputerComponent>(Subs, AtmosAlertsComputerUiKey.Key);
    }

    private void OnDeviceSilencedMessage(EntityUid uid, AtmosAlertsComputerComponent component, AtmosAlertsComputerDeviceSilencedMessage args)
    {
        if (args.SilenceDevice)
            component.SilencedDevices.Add(args.AtmosDevice);

        else
            component.SilencedDevices.Remove(args.AtmosDevice);

        Dirty(uid, component);
    }
}
