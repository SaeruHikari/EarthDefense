using Earthward.Domain;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private LocalShieldRenderer? _localShields;
    public void SyncLocalShields(IReadOnlyList<DataMap> states)
    {
        if (_localShields == null && states.Count == 0) return;
        _localShields ??= new(Globe);
        _localShields.Sync(states);
    }
}
