using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

/// <summary>
/// A <see cref="BoundUserInterfaceMessage"/> which requests the sending entity be
/// <see cref="SharedNavMapSystem.OnNavMapWarpRequest">warped to the target location</see>.
/// </summary>
/// <param name="target">The location to warp to. This is basically a <see cref="MapCoordinates"/> except the
/// <see cref="MapCoordinates.MapId"/> is inferred from the map which contains the sending entity.</param>
[Serializable, NetSerializable]
public sealed class NavMapWarpRequest(Vector2 target) : BoundUserInterfaceMessage
{
    public readonly Vector2 Target = target;
}
