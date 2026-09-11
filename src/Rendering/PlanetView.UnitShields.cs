using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private UnitShieldRenderer? _unitShields;
    private readonly Dictionary<ulong, Aabb> _frontShieldBounds = new();
    private void EnsureUnitShields()
    {
        _unitShields ??= new(SpaceRoot, Globe);
        _fleet.UnitShields = _unitShields;
    }
    /// <summary>Call after front models and combat actors are synced; accepts Dictionary.Values without allocation.</summary>
    public void SyncUnitShields(IReadOnlyCollection<DataMap> motherships)
    {
        EnsureUnitShields();
        _unitShields!.BeginCategory("motherships");
        Transform3D inverse = SpaceRoot.GlobalTransform.AffineInverse();
        foreach (var mother in motherships)
        {
            double energy = mother.N("energy_hp"), maximum = mother.N("energy_max_hp");
            if (energy <= 0 || maximum <= 0 || mother.N("hp", 1) <= 0 || mother.B("destroyed")) continue;
            if (!_frontModels.TryGetValue(mother.S("front_id"), out var visual) || !visual.Visible) continue;
            ulong modelId = visual.GetInstanceId();
            if (!_frontShieldBounds.TryGetValue(modelId, out var bounds))
                _frontShieldBounds[modelId] = bounds = UnitShieldRenderer.MeasureModelBounds(visual);
            _unitShields.Add("motherships", true, inverse * visual.GlobalTransform,
                bounds, (float)(energy / maximum), false);
        }
        _unitShields.EndCategory("motherships");
    }
    public int RenderedUnitShieldCount => _unitShields?.InstanceCount ?? 0;
    public int RenderedUnitShieldBatchCount => _unitShields?.BatchCount ?? 0;
    public IReadOnlyList<DataMap> GetUnitShieldDiagnostics() => _unitShields?.Diagnostics() ?? Array.Empty<DataMap>();
}
