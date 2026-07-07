using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// A need whose value decays over time. Examples include Thirst and Hunger.
/// </summary>
[DataDefinition, Serializable, NetSerializable, Access(typeof(SatiationSystem))]
public sealed partial class Satiation
{
    /// <summary>
    /// This satiation's type.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SatiationTypePrototype> SatiationType;

    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<SatiationPrototype> Prototype;


    /// <summary>
    /// The value of this satiation as of <see cref="LastAuthoritativeChangeTime"/>.
    /// </summary>
    /// <remarks>
    /// To get the current value at any arbitrary time, use <see cref="SatiationSystem.GetValueOrNull"/>
    /// </remarks>.
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float LastAuthoritativeValue = float.MinValue;

    /// <summary>
    /// The last time <see cref="LastAuthoritativeValue"/> was modified.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan LastAuthoritativeChangeTime;

    /// <summary>
    /// The rate at which this satiation value is expected to decay. It is a combination of
    /// <see cref="SatiationPrototype.BaseDecayRate"/> and modifiers.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float ActualDecayRate;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextDecayRateModUpdateTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextAlertUpdateTime;
}

[DataDefinition, Serializable]
public sealed partial class SatiationThresholds<T>
{
    [IncludeDataField]
    public Dictionary<SatiationValue, T> Thresholds = [];

    /// <summary>
    /// When this satiation is expected to decay from its current threshold to the next lower threshold. This
    /// is null when there is no lower threshold to decay to.
    /// </summary>
    public TimeSpan? ProjectedThresholdChangeTime;

    public T Current;
}

[TypeSerializer]
public sealed partial class SatiationThresholdsSerializer<T> : ITypeSerializer<SatiationThresholds<T>, MappingDataNode>,
    ITypeCopier<SatiationThresholds<T>>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    ) => serializationManager.ValidateNode<Dictionary<SatiationValue, T>>(node, context);

    public SatiationThresholds<T> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<SatiationThresholds<T>>? instanceProvider = null
    ) => new()
    {
        Thresholds = serializationManager.Read<Dictionary<SatiationValue, T>>(
            node,
            context,
            hookCtx.SkipHooks,
            instanceProvider is { } ip ? () => ip().Thresholds : null,
            true
        ),
    };

    public DataNode Write(
        ISerializationManager serializationManager,
        SatiationThresholds<T> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    ) => serializationManager.WriteValue(value.Thresholds, alwaysWrite, context, true);

    public void CopyTo(
        ISerializationManager serializationManager,
        SatiationThresholds<T> source,
        ref SatiationThresholds<T> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null
    ) => serializationManager.CopyTo(source.Thresholds, ref target.Thresholds, context, true, true);
}
