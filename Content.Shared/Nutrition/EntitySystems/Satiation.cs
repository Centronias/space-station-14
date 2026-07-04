using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
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

[DataDefinition]
public sealed partial class SatiationThresholds<T>
{
    [DataField(SatiationThresholds.ThresholdsId)]
    public Dictionary<SatiationValue, T> Thresholds = [];

    /// <summary>
    /// When this satiation is expected to decay from its current threshold to the next lower threshold. This
    /// is null when there is no lower threshold to decay to.
    /// </summary>
    public TimeSpan? ProjectedThresholdChangeTime;

    public T Current;
}

public static class SatiationThresholds
{
    public const string ThresholdsId = "thresholds";
}

[DataRecord]
public sealed partial record SatiationTypeToThresholdsDict<T>
{
    public Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> Satiations = new();
}

public sealed partial class SatiationThresholdsSerializer<T> : ITypeSerializer<SatiationThresholds<T>, MappingDataNode>,
    ITypeCopier<SatiationThresholds<T>>
{
    private readonly DictionarySerializer<SatiationValue, T> _delegateSerializer = new();

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    )
    {
        var mapping = new MappingDataNode(1);

        if (!node.TryGetValue(SatiationThresholds.ThresholdsId, out var value))
            return new FieldNotFoundErrorNode(mapping.GetKeyNode(SatiationThresholds.ThresholdsId), typeof(T));

        if (value is MappingDataNode mappingValue)
            return _delegateSerializer.Validate(serializationManager, mappingValue, dependencies, context);

        return new ErrorNode(mapping.GetKeyNode(SatiationThresholds.ThresholdsId),
            $"Invalid node type {value.GetType()}, expected {typeof(MappingDataNode)}");
    }

    public SatiationThresholds<T> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<SatiationThresholds<T>>? instanceProvider = null
    )
    {
        var ret = instanceProvider != null ? instanceProvider() : new SatiationThresholds<T>();

        if (node.TryGetValue(SatiationThresholds.ThresholdsId, out var value) && value is MappingDataNode mappingValue)
        {
            ret.Thresholds = _delegateSerializer.Read(
                serializationManager,
                mappingValue,
                dependencies,
                hookCtx,
                context,
                (ISerializationManager.InstantiationDelegate<Dictionary<SatiationValue, T>>?)null
            );
        }
        else
        {
            ret.Thresholds = [];
        }

        return ret;
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        SatiationThresholds<T> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    ) => new MappingDataNode(1)
    {
        [SatiationThresholds.ThresholdsId] = _delegateSerializer.Write(
            serializationManager,
            value.Thresholds,
            dependencies,
            alwaysWrite: false,
            context
        ),
    };

    public void CopyTo(
        ISerializationManager serializationManager,
        SatiationThresholds<T> source,
        ref SatiationThresholds<T> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null
    ) => _delegateSerializer.CopyTo(
        serializationManager,
        source.Thresholds,
        ref target.Thresholds,
        dependencies,
        hookCtx,
        context
    );
}

public sealed class DictionaryOfSatiationTypeProtoIdToSatiationThresholdsSerializer<T> : ITypeSerializer<
    Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>>,
    MappingDataNode
>, ITypeCopier<Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>>>
{
    private readonly PrototypeIdSerializer<SatiationTypePrototype> _keySerializer = new();
    private readonly SatiationThresholdsSerializer<T> _valuesSerializer = new();

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    )
    {
        var mapping = new Dictionary<ValidationNode, ValidationNode>();
        foreach (var (key, val) in node.Children)
        {
            mapping.Add(
                _keySerializer.Validate(serializationManager, node.GetKeyNode(key), dependencies, context),
                val is MappingDataNode valMapping
                    ? _valuesSerializer.Validate(serializationManager, valMapping, dependencies, context)
                    : new ErrorNode(val, $"Value should be ${nameof(MappingDataNode)}, but is actually {val.GetType()}")
            );
        }

        return new ValidatedMappingNode(mapping);
    }

    public Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<
            Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>>
        >? instanceProvider = null
    )
    {
        var ret = instanceProvider?.Invoke() ??
                  new Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>>();
        foreach (var (k, v) in node.Children)
        {
            if (v is not MappingDataNode valMapping)
                continue;

            ret[k] = _valuesSerializer.Read(serializationManager, valMapping, dependencies, hookCtx, context);
        }

        return ret;
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    )
    {
        var ret = new MappingDataNode();
        foreach (var (k, v) in value)
        {
            ret[k.Id] = _valuesSerializer.Write(serializationManager, v, dependencies, alwaysWrite, context);
        }

        return ret;
    }

    public void CopyTo(
        ISerializationManager serializationManager,
        Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> source,
        ref Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<T>> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null
    )
    {
        target.Clear();
        foreach (var (k, v) in source)
        {
            var newValue = new SatiationThresholds<T>();
            _valuesSerializer.CopyTo(serializationManager, v, ref newValue, dependencies, hookCtx, context);
            target[k] = newValue;
        }
    }
}

[TypeSerializer]
public sealed partial class SatiationTypeToThresholdsDictSerializer<T> : ITypeSerializer<
    SatiationTypeToThresholdsDict<T>,
    MappingDataNode
>, ITypeCopier<SatiationTypeToThresholdsDict<T>>
{
    private readonly DictionaryOfSatiationTypeProtoIdToSatiationThresholdsSerializer<T> _delegate = new();

    public ValidationNode Validate(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    ) => _delegate.Validate(serializationManager, node, dependencies, context);

    public SatiationTypeToThresholdsDict<T> Read(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<SatiationTypeToThresholdsDict<T>>? instanceProvider = null)
    {
        var ret = instanceProvider?.Invoke() ?? new SatiationTypeToThresholdsDict<T>();
        ret.Satiations = _delegate.Read(serializationManager, node, dependencies, hookCtx, context);
        return ret;
    }

    public DataNode Write(ISerializationManager serializationManager,
        SatiationTypeToThresholdsDict<T> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    ) => _delegate.Write(serializationManager, value.Satiations, dependencies, alwaysWrite, context);

    public void CopyTo(
        ISerializationManager serializationManager,
        SatiationTypeToThresholdsDict<T> source,
        ref SatiationTypeToThresholdsDict<T> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null
    ) => _delegate.CopyTo(
        serializationManager,
        source.Satiations,
        ref target.Satiations,
        dependencies,
        hookCtx,
        context
    );
}
