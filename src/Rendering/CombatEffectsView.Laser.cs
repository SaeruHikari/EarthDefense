using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

public sealed partial class CombatEffectsView
{
    private EffectInstanceBatch? _laserRibbons, _laserFlares;
    public int LaserRibbonCount => _laserRibbons?.Count ?? 0;
    public int LaserFlareCount => _laserFlares?.Count ?? 0;
    private int LaserBatchCount => (_laserRibbons == null ? 0 : 1) + (_laserFlares == null ? 0 : 1);
    private long LaserBufferUploads => (_laserRibbons?.BufferUploads ?? 0) + (_laserFlares?.BufferUploads ?? 0);
    private long LaserNativeWrites => (_laserRibbons?.NativeWrites ?? 0) + (_laserFlares?.NativeWrites ?? 0);
    private static ArrayMesh LaserQuad(bool endpoint)
    {
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        float bottom = endpoint ? -1 : 0;
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(-1, bottom, 0), new(1, bottom, 0), new(-1, 1, 0), new(-1, 1, 0), new(1, bottom, 0), new(1, 1, 0) };
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { new(0, 0), new(1, 0), new(0, 1), new(0, 1), new(1, 0), new(1, 1) };
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
    private void EnsureLaserResources()
    {
        if (_laserRibbons != null) return;
        var ribbon = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/laser_glow.gdshader") };
        var flare = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/laser_flare.gdshader") };
        _laserRibbons = new(_space, "LaserRibbonBatch", LaserQuad(false), ribbon);
        _laserFlares = new(_space, "LaserEndpointBatch", LaserQuad(true), flare);
    }
    private void BeginLaserBeams() { _laserRibbons?.Begin(); _laserFlares?.Begin(); }
    private void CommitLaserBeams() { _laserRibbons?.Commit(); _laserFlares?.Commit(); }
    private void ClearLaserBeams() { _laserRibbons?.Clear(); _laserFlares?.Clear(); }
    private void AddLaserVisual(DataMap beam, Vector3 a, Vector3 b, Basis cameraBasis)
    {
        if (!a.IsFinite() || !b.IsFinite() || a.DistanceSquaredTo(b) < .000001f) return;
        float fraction = Mathf.Clamp((float)(beam.N("life", .17) / Math.Max(.00001, beam.N("max_life", .17))), 0, 1);
        if (fraction <= 0) return;
        float fade = fraction * fraction * (3 - 2 * fraction);
        Color tint = beam.Get<Color>("color", new("b48cff")); tint.A *= fade;
        EnsureLaserResources();
        Vector3 along = b - a;
        Vector3 side = along.Normalized().Cross(cameraBasis.Z);
        if (side.LengthSquared() < .0001f) side = cameraBasis.X;
        side = side.Normalized() * .026f;
        _laserRibbons!.Add(BeamTransform(a, b, side), tint);
        _laserRibbons.IncludeBounds(a.Min(b) - side.Abs(), a.Max(b) + side.Abs());
        AddLaserFlare(a, .105f, tint, cameraBasis);
        AddLaserFlare(b, .145f, tint, cameraBasis);
    }
    private void AddLaserFlare(Vector3 at, float radius, Color tint, Basis cameraBasis)
    {
        Vector3 right = cameraBasis.X.Normalized() * radius, up = cameraBasis.Y.Normalized() * radius;
        var transform = new Transform3D(new Basis(right, up, cameraBasis.Z.Normalized()), at);
        _laserFlares!.Add(transform, tint);
        Vector3 extent = right.Abs() + up.Abs();
        _laserFlares.IncludeBounds(at - extent, at + extent);
    }
}
