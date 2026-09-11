using Godot;
namespace Earthward.Rendering;

public sealed class FacilityVisual
{
    public Node3D Root
    {
        get;
    }
    public string Kind
    {
        get;
    }
    private readonly Dictionary<string, Node3D> _parts = new();
    private readonly float _phaseOffset;
    public FacilityVisual(Node3D parent, string kind)
    {
        Kind = kind;
        Root = kind == "shield" ? ShieldProjectorModel.Create() : RenderAssets.Instantiate($"res://assets/managed/factories/{kind}.scn");
        parent.AddChild(Root);
        foreach (var node in Root.FindChildren("*", "Node3D", true, false))
            if (node is Node3D p)
                _parts[p.Name] = p;
        _phaseOffset = Mathf.PosMod(Root.GlobalPosition.Dot(new(7.19f, 13.73f, 5.51f)), Mathf.Tau);
    }
    public bool IsFactory => Kind is "interceptor" or "missile" or "laser";
    public Vector3 LaunchPosition => Root.ToGlobal(new(0, .106f, .045f));
    public Vector3 LaunchDirection => Root.GlobalBasis.Z.Normalized();
    public void SetActivity(float phase, float progress)
    {
        phase = Mathf.Clamp(phase, 0, 1);
        progress = Mathf.Clamp(progress, 0, 1);
        if (_parts.TryGetValue("LeftDoor", out var left))
            left.Position = new(-.062f - phase * .126f, .111f, .132f);
        if (_parts.TryGetValue("RightDoor", out var right))
            right.Position = new(.062f + phase * .126f, .111f, .132f);
        if (_parts.TryGetValue("ProductionProgress", out var strip))
        {
            strip.Scale = new(Math.Max(.005f, progress), 1, 1);
            strip.Position = new((progress - 1) * .07f, .224f, .051f);
        }
    }
    public void Update(double time)
    {
        float t = (float)time + _phaseOffset;
        if (_parts.TryGetValue("ProductionCoolingRotor", out var rotor))
            rotor.Rotation = new(0, Mathf.PosMod((float)time * (Kind == "laser" ? 2.6f : 2.1f), Mathf.Tau), 0);
        if (Kind == "shield" && _parts.TryGetValue("ShieldEmitterRing", out var emitter))
            emitter.Rotation = new(.24f * Mathf.Sin(t * .7f), Mathf.PosMod(t * .55f, Mathf.Tau), .18f * Mathf.Cos(t * .7f));
        if (Kind == "mine")
        {
            if (_parts.TryGetValue("RotatingDrill", out var drill))
            {
                drill.Position = new(0, .146f, 0);
                drill.Rotation = new(0, Mathf.PosMod(t * 1.7f, Mathf.Tau), 0);
            }
            if (_parts.TryGetValue("ReciprocatingPump", out var pump))
                pump.Position = new(.097f, .148f + Mathf.Sin(t * 2.2f) * .012f, .037f);
        }
        else if (Kind == "solar" && _parts.TryGetValue("TrackingSolarWings", out var solar))
        {
            solar.Position = new(0, .177f, 0);
            solar.Rotation = new(-.27f + Mathf.Sin(t * .12f) * .11f, Mathf.Sin(t * .08f) * .15f, 0);
        }
        else if (Kind == "lab" && _parts.TryGetValue("ResearchInstrumentRing", out var lab))
        {
            lab.Position = new(0, .225f, 0);
            lab.Rotation = new(0, Mathf.PosMod(t * .45f, Mathf.Tau), 0);
        }
    }
}
