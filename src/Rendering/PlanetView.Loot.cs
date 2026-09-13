using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private LootRenderer? _loot;

    /// <summary>Present only world-phase pickups. Call after SpaceRoot and the camera are ready.</summary>
    public void SyncLoot(IReadOnlyList<DataMap> pickups, double uiTime, long hoveredUid)
    {
        if (SpaceRoot == null || Camera == null || !GodotObject.IsInstanceValid(SpaceRoot)) return;
        if (_loot == null && pickups.Count == 0) return;
        _loot ??= new LootRenderer(SpaceRoot);
        _loot.Sync(pickups, Camera, ViewSize, uiTime, hoveredUid);
    }

    public int RenderedLootCount => _loot?.InstanceCount ?? 0;
    public int RenderedLootBatchCount => _loot?.BatchCount ?? 0;
    public IReadOnlyList<DataMap> GetLootDiagnostics() => _loot?.Diagnostics() ?? Array.Empty<DataMap>();
}
