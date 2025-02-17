using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

// TODO namespace is wrong.
namespace Content.Shared.Wrapping;

[RegisterComponent]
[Access] // Readonly, except for VV editing
public sealed partial class SpawnAndInsertOnInteractComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ToSpawnId;

    [DataField(required: true)]
    public string ContainerId = string.Empty;

    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField(required: true)]
    public LocId InteractVerb = new();

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public int? ChargesConsumed = null;

    [DataField(required: true)]
    public EntityWhitelist? Whitelist = new();

    [DataField(required: true)]
    public EntityWhitelist? Blacklist = new();
}

public sealed partial class SpawnAndInsertOnInteractionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnAndInsertOnInteractComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SpawnAndInsertOnInteractComponent, GetVerbsEvent<UtilityVerb>>(OnGetUtilityVerbs);
        SubscribeLocalEvent<SpawnAndInsertOnInteractComponent, DoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<SpawnAndInsertOnInteractComponent> spawner, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            args.Target is not { } target ||
            !args.CanReach ||
            !IsApplicable(spawner, target))
            return;

        args.Handled = SpawnAndInsertOrStartDoAfter(args.User, spawner, target);
    }

    private void OnGetUtilityVerbs(Entity<SpawnAndInsertOnInteractComponent> spawner,
        ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsApplicable(spawner, args.Target))
            return;

        // "Capture" the values from `args` because C# doesn't like doing the capturing for `ref` values.
        var user = args.User;
        var target = args.Target;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString(spawner.Comp.InteractVerb),
            IconEntity = GetNetEntity(spawner),
            Act = () => SpawnAndInsertOrStartDoAfter(user, spawner, target),
        });
    }

    private void OnDoAfter(Entity<SpawnAndInsertOnInteractComponent> spawner, ref DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is { } target)
        {
            SpawnAndInsert(args.User, spawner, target);

            args.Handled = true;
        }
    }

    public bool IsApplicable(Entity<SpawnAndInsertOnInteractComponent> spawner, EntityUid target)
    {
        return
            // Spawner cannot be applied to itself.
            spawner.Owner != target &&
            // Spawner has sufficient charges...
            (spawner.Comp.ChargesConsumed is not { } charges ||
             // ... or doesn't use charges.
             !_charges.HasInsufficientCharges(spawner, charges)) &&
            _whitelist.IsWhitelistPass(spawner.Comp.Whitelist, target) &&
            _whitelist.IsBlacklistFail(spawner.Comp.Blacklist, target);
    }

    private bool SpawnAndInsertOrStartDoAfter(EntityUid user,
        Entity<SpawnAndInsertOnInteractComponent> spawner,
        EntityUid target)
    {
        if (spawner.Comp.Delay == TimeSpan.Zero)
        {
            SpawnAndInsert(user, spawner, target);
            return true;
        }

        return _doAfter.TryStartDoAfter(
            new DoAfterArgs(EntityManager,
                user,
                spawner.Comp.Delay,
                new DoAfterEvent(),
                spawner,
                target,
                spawner)
            {
                NeedHand = true,
                BreakOnMove = true,
                BreakOnDamage = true,
            }
        );
    }

    private void SpawnAndInsert(EntityUid user, Entity<SpawnAndInsertOnInteractComponent> spawner, EntityUid target)
    {
        if (_net.IsServer)
        {
            var spawned = Spawn(spawner.Comp.ToSpawnId, Transform(target).Coordinates);

            // If the target's in a container, try to put the spawned entity in its place in the container.
            if (_container.TryGetContainingContainer((target, null, null), out var containerOfTarget))
            {
                // Remove the target to make space, then put the spawned entity in.
                _container.Remove(target, containerOfTarget);
                _container.InsertOrDrop((spawned, null, null), containerOfTarget);
            }

            // Insert the target into the spawned entity.
            if (!_container.TryGetContainer(spawned, spawner.Comp.ContainerId, out var container) ||
                !_container.Insert(target, container))
            {
                DebugTools.Assert(
                    $"Failed to insert target entity into newly spawned container. target={PrettyPrint.PrintUserFacing(target)}");
                QueueDel(spawned);
            }
        }

        if (spawner.Comp.ChargesConsumed is { } consume &&
            TryComp<LimitedChargesComponent>(spawner, out var charges) &&
            !_charges.HasInsufficientCharges(spawner, consume, charges))
        {
            _charges.UseCharges(spawner, consume, charges);
        }

        _audio.PlayPredicted(spawner.Comp.Sound, target, user);
    }

    [Serializable, NetSerializable]
    private sealed partial class DoAfterEvent : SimpleDoAfterEvent;
}
