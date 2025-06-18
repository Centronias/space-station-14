using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared.Storage.EntitySystems;

/// <summary>
/// This system implements <see cref="QuickPickupComponent"/>'s behavior.
/// </summary>
public sealed partial class QuickPickupSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private EntityQuery<ItemComponent> _itemQuery;

    private const string DelayId = "quickPickup";

    public override void Initialize()
    {
        _itemQuery = GetEntityQuery<ItemComponent>();

        SubscribeLocalEvent<QuickPickupComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<QuickPickupComponent, AfterInteractEvent>(
            AfterInteract,
            before: [typeof(AreaPickupSystem)]
        );
    }

    private void OnMapInit(Entity<QuickPickupComponent> entity, ref MapInitEvent args)
    {
        _useDelay.SetLength(entity.Owner, entity.Comp.Cooldown, DelayId);
    }

    private void AfterInteract(Entity<QuickPickupComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            args.Target is not { Valid: true } target ||
            target == args.User ||
            !args.CanReach ||
            !_useDelay.TryResetDelay(entity, checkDelayed: true, id: DelayId) ||
            _container.IsEntityInContainer(target) ||
            !_itemQuery.HasComponent(target))
            return;

        var ev = new QuickPickupEvent(GetNetEntity(entity), GetNetEntity(target), GetNetEntity(args.User));
        RaiseLocalEvent(entity, ev);
        args.Handled = ev.Handled;
    }

    /// <summary>
    /// This function helps with handling <see cref="QuickPickupEvent"/> by handling
    /// <see cref="AnimateInsertingEntitiesEvent">animating picked up entities</see> while also invoking
    /// <paramref name="tryPickup"/>. Returns true if the entity in <paramref name="ev"/> was actually picked up, false
    /// otherwise.
    /// </summary>
    public bool TryDoQuickPickup(QuickPickupEvent ev, Func<bool> tryPickup)
    {
        var quickPickupEntity = GetEntity(ev.QuickPickupEntity);
        var pickedUp = GetEntity(ev.PickedUp);

        if (!TryComp<QuickPickupComponent>(quickPickupEntity, out var quickPickup) ||
            !TryComp(quickPickupEntity, out TransformComponent? pickupEntityXform) ||
            !TryComp(pickedUp, out TransformComponent? targetXform))
            return false;

        var user = GetEntity(ev.User);

        _projectile.EmbedDetach(pickedUp, null, user);

        // Get the picked up entity's position _before_ inserting it, because that changes its position.
        var position = _transform.ToCoordinates(
            pickupEntityXform.ParentUid.IsValid() ? pickupEntityXform.ParentUid : quickPickupEntity,
            _transform.GetMapCoordinates(targetXform)
        );

        if (tryPickup())
        {
            EntityManager.RaiseSharedEvent(
                new AnimateInsertingEntitiesEvent(
                    GetNetEntity(quickPickupEntity),
                    new List<NetEntity> { GetNetEntity(pickedUp) },
                    new List<NetCoordinates> { GetNetCoordinates(position) },
                    new List<Angle> { pickupEntityXform.LocalRotation }
                ),
                user
            );
            return true;
        }

        return false;
    }
}
