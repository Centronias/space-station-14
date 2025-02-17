using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Materials;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

// TODO namespace is wrong.
namespace Content.Shared.Wrapping;

[RegisterComponent]
[Access] // Default to readonly, except for VV editing
public sealed partial class FrangibleContainerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), Access(typeof(FrangibleContainerSystem))]
    public ContainerSlot Contents = default!;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string ContainerId = "frangible_container_contents";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId? StartingItem;

    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField(required: true)]
    public LocId OpenVerb = new();

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public bool OpenOnInteract;

    [DataField]
    public bool OpenOnUse;

    [DataField]
    public bool OpenOnBreak;

    [DataField]
    public bool OpenOnReclaimed;

    [DataField]
    public bool OpenOnDestroyed;

    [DataField]
    public EntProtoId? TrashProtoId = null;
}

public sealed partial class FrangibleContainerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<FrangibleContainerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<FrangibleContainerComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<FrangibleContainerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FrangibleContainerComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<FrangibleContainerComponent, DoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent(DestroyAndReleaseOn<BreakageEventArgs>(c => c.OpenOnBreak));
        SubscribeLocalEvent(DestroyAndReleaseOn<GotReclaimedEvent>(c => c.OpenOnReclaimed));
        SubscribeLocalEvent(DestroyAndReleaseOn<DestructionEventArgs>(c => c.OpenOnDestroyed));
    }

    private void OnComponentInit(Entity<FrangibleContainerComponent> frangible, ref ComponentInit args)
    {
        frangible.Comp.Contents = _container.EnsureContainer<ContainerSlot>(frangible, frangible.Comp.ContainerId);

        if (frangible.Comp.StartingItem is { } startingItem)
        {
            _container.Insert((Spawn(startingItem), null, null), frangible.Comp.Contents, force: true);
        }
    }

    private void OnUseInHand(Entity<FrangibleContainerComponent> frangible, ref UseInHandEvent args)
    {
        if (args.Handled || !frangible.Comp.OpenOnUse)
            return;

        args.Handled = DestroyAndReleaseOrTryStartDoAfter(args.User, frangible);
    }

    private void OnInteractHand(Entity<FrangibleContainerComponent> frangible, ref InteractHandEvent args)
    {
        if (args.Handled || !frangible.Comp.OpenOnInteract)
            return;

        args.Handled = DestroyAndReleaseOrTryStartDoAfter(args.User, frangible);
    }

    private void OnGetInteractionVerbs(Entity<FrangibleContainerComponent> frangible,
        ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands is null)
            return;

        // "Capture" the values from `args` because C# doesn't like doing the capturing for `ref` values.
        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(frangible.Comp.OpenVerb),
            Act = () => DestroyAndReleaseOrTryStartDoAfter(user, frangible),
        });
    }

    private void OnDoAfter(Entity<FrangibleContainerComponent> frangible, ref DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        DestroyAndReleaseContents(args.User, frangible);
        args.Handled = true;
    }

    /// <summary>
    /// Event-Type parameterized handler which unwraps the given frangible if <paramref name="condition"/> is true.
    /// </summary>
    private EntityEventRefHandler<FrangibleContainerComponent, T> DestroyAndReleaseOn<T>(
        Func<FrangibleContainerComponent, bool> condition)
        where T : notnull
    {
        return (Entity<FrangibleContainerComponent> frangible, ref T _) =>
        {
            if (condition(frangible))
                DestroyAndReleaseContents(null, frangible);
        };
    }

    private bool DestroyAndReleaseOrTryStartDoAfter(EntityUid user, Entity<FrangibleContainerComponent> frangible)
    {
        if (frangible.Comp.Delay == TimeSpan.Zero)
        {
            DestroyAndReleaseContents(user, frangible);
            return true;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            frangible.Comp.Delay,
            new DoAfterEvent(),
            frangible,
            frangible)
        {
            NeedHand = true,
        });
    }

    private EntityUid? DestroyAndReleaseContents(EntityUid? user, Entity<FrangibleContainerComponent> frangible)
    {
        var containedEntity = frangible.Comp.Contents.ContainedEntity;
        _audio.PlayPredicted(frangible.Comp.Sound, frangible, user);

        if (_net.IsClient)
            return containedEntity;

        var frangibleTransform = Transform(frangible);

        if (containedEntity is { } contents)
        {
            _container.Remove(contents, frangible.Comp.Contents, true, true, frangibleTransform.Coordinates);

            // If the frangible's in a container, try to put the unwrapped contents in that container.
            if (_container.TryGetContainingContainer((frangible, null, null), out var outerContainer))
            {
                _container.Remove((frangible, null, null), outerContainer, force: true);
                _container.InsertOrDrop((contents, null, null), outerContainer);
            }
        }

        // Spawn unwrap trash.
        if (frangible.Comp.TrashProtoId is { } trashProto)
        {
            var trash = Spawn(trashProto, frangibleTransform.Coordinates);
            _transform.DropNextTo((trash, null), (frangible, frangibleTransform));
        }

        QueueDel(frangible);

        return containedEntity;
    }


    [Serializable, NetSerializable]
    private sealed partial class DoAfterEvent : SimpleDoAfterEvent;
}
