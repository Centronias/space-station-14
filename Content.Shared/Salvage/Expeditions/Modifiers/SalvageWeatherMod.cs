using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Salvage.Expeditions.Modifiers;

[Prototype("salvageWeatherMod")]
public sealed partial class SalvageWeatherMod : IPrototype, IBiomeSpecificMod
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("desc")] public LocId Description { get; private set; } = string.Empty;

    /// <inheritdoc/>
    [DataField("cost")]
    public float Cost { get; private set; } = 0f;

    /// <inheritdoc/>
    [DataField]
    public List<ProtoId<SalvageBiomeModPrototype>>? Biomes { get; private set; } = null;

    /// <summary>
    /// Weather prototype to use on the planet.
    /// </summary>
    [DataField("weather", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<WeatherPrototype>))]
    public string WeatherPrototype = string.Empty;
}
