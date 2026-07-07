using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// This abstract system provides a convenient interface for implementing effects which react to changes in
/// <see cref="Satiation"/> thresholds.
/// </summary>
public abstract partial class BaseSatiationEffectSystem<TComp, T> : EntitySystem where TComp : Component
{
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private EntityQuery<SatiationComponent> _satiationQuery;
    [Dependency] private IGameTiming _timing = default!;

    protected abstract Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> GetThresholds(TComp comp);
    protected abstract T DefaultValue();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TComp, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TComp, SatiationUpdateEvent>(OnSatiationUpdate);
    }

    public override void Update(float frameTime)
    {
        var q = EntityQueryEnumerator<TComp, SatiationComponent>();
        while (q.MoveNext(out var ent, out var comp, out var satiation))
        {
            foreach (var (type, thresholds) in GetThresholds(comp))
            {
                if (_timing.CurTime < thresholds.ProjectedThresholdChangeTime)
                    continue;

                UpdateSatiation((ent, comp), satiation, type);
            }
        }
    }

    [MustCallBase]
    protected virtual void OnMapInit(Entity<TComp> entity, ref MapInitEvent args)
    {
        // Make sure we have a satiation component. Realistically, this just exists to cause test failures if an entity
        // with `TComp` doesn't have a `SatiationComponent`.
        var comp = EnsureComp<SatiationComponent>(entity);
        foreach (var type in GetThresholds(entity.Comp).Keys)
        {
            UpdateSatiation(entity, comp, type);
        }
    }

    [MustCallBase]
    protected void OnSatiationUpdate(Entity<TComp> entity, ref SatiationUpdateEvent args)
    {
        if (!_satiationQuery.TryComp(entity, out var comp))
            return;

        UpdateSatiation(entity, comp, args.Type);
    }

    private void UpdateSatiation(Entity<TComp> entity, SatiationComponent comp, ProtoId<SatiationTypePrototype> type)
    {
        if (!GetThresholds(entity.Comp).TryGetValue(type, out var thresholds))
            return;

        if (_satiation.TryGetValueByThreshold(
                (entity, comp),
                type,
                thresholds.Thresholds,
                out var result,
                out var nextLowerThreshold))
        {
            thresholds.Current = result ?? DefaultValue();
            thresholds.ProjectedThresholdChangeTime = nextLowerThreshold is { } lower
                ? _satiation.GetTimeToDecay((entity, comp), type, lower)
                : null;
        }
        else
        {
            thresholds.Current = DefaultValue();
            thresholds.ProjectedThresholdChangeTime = null;
        }

        Dirty(entity);

        AfterSatiationUpdate(entity);
    }

    protected virtual void AfterSatiationUpdate(Entity<TComp> entity) { }
}
