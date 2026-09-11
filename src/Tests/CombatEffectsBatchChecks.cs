using Godot;
using System.Diagnostics;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

public partial class CombatEffectsBatchChecks : Node
{
    private int _checks, _failures;
    private SubViewport _view = null!;
    private Node3D _scene = null!, _legacySpace = null!, _legacySurface = null!, _batchSpace = null!, _batchSurface = null!;
    private Camera3D _camera = null!;
    private CombatEffectsView? _effects;
    private LegacyCombatEffectsReference? _reference;
    private static readonly Vector3[] QuadVertices = [new(-1, 0, 0), new(1, 0, 0), new(-1, 1, 0), new(-1, 1, 0), new(1, 0, 0), new(1, 1, 0)];
    private void Check(bool value, string label) { _checks++; if (!value) { _failures++; GD.PrintErr("EFFECTS_BATCH_FAIL: " + label); } }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void SetupScene()
    {
        _view = new SubViewport { Size = new Vector2I(640, 480), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_view);
        _scene = new Node3D(); _view.AddChild(_scene);
        var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.003f, .006f, .012f), AmbientLightEnergy = 0, GlowEnabled = true, GlowIntensity = .9f, TonemapMode = Godot.Environment.ToneMapper.Filmic };
        _scene.AddChild(new WorldEnvironment { Environment = environment });
        _camera = new Camera3D { Position = new Vector3(0, 0, 24), Fov = 42, Near = .05f, Far = 100 };
        _scene.AddChild(_camera); _camera.LookAt(new Vector3(0, 0, 16.4f)); _camera.MakeCurrent();
        _legacySpace = new Node3D { Name = "LegacySpace" }; _legacySurface = new Node3D { Name = "LegacySurface" };
        _batchSpace = new Node3D { Name = "BatchSpace" }; _batchSurface = new Node3D { Name = "BatchSurface" };
        foreach (var root in new[] { _legacySpace, _legacySurface, _batchSpace, _batchSurface }) _scene.AddChild(root);
        _effects = new CombatEffectsView(_batchSpace, _batchSurface, _camera);
        Check(_effects.BatchCount == 0 && _batchSpace.GetChildCount() == 0 && _batchSurface.GetChildCount() == 0, "empty constructor creates no native effect resources");
        _effects.Sync([], []); _effects.Clear();
        Check(_effects.BatchCount == 0 && _effects.BufferUploads == 0 && _effects.NativeWrites == 0, "empty sync and clear create no resources or uploads");
        _reference = new LegacyCombatEffectsReference(_legacySpace, _legacySurface, _camera);
    }
    private static List<DataMap> Bursts(int count)
    {
        var result = new List<DataMap>();
        for (int i = 0; i < count; i++) result.Add(new DataMap
        {
            ["uid"] = 173L + i * 7, ["space_position"] = new Vector3((i % 3 - 1) * 1.5f, (i / 3 % 3 - 1) * 1.4f, 16.5f + i % 2 * .05f),
            ["age"] = .2, ["life"] = 1d, ["radius_world"] = .58d + i % 3 * .04,
            ["color"] = i % 3 == 0 ? new Color(.12f, .64f, .95f, .35f) : i % 3 == 1 ? new Color("f29462") : new Color("a1ffe5")
        });
        return result;
    }
    private static List<DataMap> Beams() => new()
    {
        new() { ["from_space"] = new Vector3(-2.4f, 1.95f, 16.7f), ["to_space"] = new Vector3(2.4f, 1.95f, 16.7f), ["life"] = .07, ["color"] = new Color("71e4c1") },
        new() { ["from_space"] = new Vector3(-2.2f, -1.8f, 15.9f), ["to_space"] = new Vector3(-2.2f, -1.8f, 17.5f), ["life"] = .025, ["color"] = new Color(.7f, .2f, .9f, .7f) },
        new() { ["from"] = Vector3.Back.Rotated(Vector3.Up, -.12f).Rotated(Vector3.Right, .12f), ["to"] = Vector3.Back.Rotated(Vector3.Up, .12f).Rotated(Vector3.Right, .12f), ["life"] = .11, ["color"] = new Color("ef9651") },
        new() { ["from"] = Vector3.Back, ["to"] = Vector3.Back.Rotated(Vector3.Up, .007f), ["life"] = .04, ["color"] = new Color("64b9ee") }
    };
    private void InspectBursts(List<DataMap> bursts)
    {
        var core = _effects!.GetBatchNode("cores")!.Multimesh;
        var shell = _effects.GetBatchNode("shells")!.Multimesh;
        var sparks = _effects.GetBatchNode("sparks")!.Multimesh;
        Check(_effects.BurstCount == bursts.Count && core.VisibleInstanceCount == bursts.Count && shell.VisibleInstanceCount == bursts.Count && sparks.VisibleInstanceCount == bursts.Count * 8, "all cores shells and eight sparks remain rendered");
        foreach (var data in bursts)
        {
            long uid = data.L("uid"); int index = _effects.BurstInstanceIndex(uid);
            float t = Mathf.Clamp((float)(data.N("age") / data.N("life")), 0, 1), radius = (float)data.N("radius_world");
            var position = data.Vector3("space_position"); var color = data.Get<Color>("color");
            Transform3D c = core.GetInstanceTransform(index), s = shell.GetInstanceTransform(index);
            Check(c.Origin.IsEqualApprox(position) && Math.Abs(c.Basis.X.Length() - Math.Max(.001f, radius * .28f * Mathf.Pow(1 - t, 2))) < .00001f, "core position and scale match original curve");
            Check(s.Origin.IsEqualApprox(position) && Math.Abs(s.Basis.X.Length() - Math.Max(.001f, radius * (.15f + .85f * Mathf.Sqrt(t)))) < .00001f, "shell expansion matches original curve");
            Color cc = core.GetInstanceCustomData(index), sc = shell.GetInstanceCustomData(index), sparkColor = sparks.GetInstanceCustomData(index * 8);
            var expectedCore = color.Lerp(Colors.White, .38f).SrgbToLinear(); var expected = color.SrgbToLinear();
            Check(new Vector3(cc.R, cc.G, cc.B).IsEqualApprox(new Vector3(expectedCore.R, expectedCore.G, expectedCore.B)) && Math.Abs(cc.A - Mathf.Pow(1 - t, 3)) < .00001f, "core color and visibility preserve instance-uniform semantics");
            Check(new Vector3(sc.R, sc.G, sc.B).IsEqualApprox(new Vector3(expected.R, expected.G, expected.B)) && Math.Abs(sc.A - Mathf.Pow(1 - t, 1.7f) * .6f) < .00001f && Math.Abs(sparkColor.A - Mathf.Pow(1 - t, 1.4f)) < .00001f, "shell and spark alpha curves remain unchanged");
            for (int i = 0; i < 8; i++)
            {
                float y = 1 - 2 * (i + .5f) / 8, angle = i * 2.39996323f + uid % 19 * .31f, ring = Mathf.Sqrt(1 - y * y);
                var direction = new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
                var expectedTransform = new Transform3D(Basis.LookingAt(direction, Vector3.Up).Scaled(Vector3.One * (1 - t * .6f)), position + direction * radius * t * (.8f + i % 3 * .22f));
                Check(sparks.GetInstanceTransform(index * 8 + i).IsEqualApprox(expectedTransform), "UID-derived spark direction basis distance and scale remain exact");
            }
        }
    }
    private void InspectBeams(List<DataMap> beams)
    {
        int world = 0, surface = 0;
        void Quad(bool inWorld, Vector3 a, Vector3 b, Vector3 width, Color color)
        {
            int index = inWorld ? world++ : surface++;
            var mm = _effects!.GetBatchNode(inWorld ? "world_beams" : "surface_beams")!.Multimesh;
            var transform = mm.GetInstanceTransform(index);
            Vector3[] expected = [a - width, a + width, b - width, b - width, a + width, b + width];
            Check(Enumerable.Range(0, 6).All(i => (transform * QuadVertices[i]).DistanceTo(expected[i]) < .00001f), "unit quad reconstructs every original beam vertex");
            Check(mm.GetInstanceCustomData(index).IsEqualApprox(color), "beam color and life alpha preserved");
        }
        foreach (var beam in beams)
        {
            var color = beam.Get<Color>("color"); color.A *= Mathf.Clamp((float)beam.N("life") * 7, .12f, 1);
            if (beam.ContainsKey("from_space"))
            {
                Vector3 a = beam.Vector3("from_space"), b = beam.Vector3("to_space"), width = (b - a).Normalized().Cross(_camera.GlobalBasis.Z);
                if (width.LengthSquared() < .0001f) width = _camera.GlobalBasis.X;
                Quad(true, a, b, width.Normalized() * .009f, color);
            }
            else
            {
                Vector3 a = beam.Vector3("from").Normalized(), b = beam.Vector3("to").Normalized();
                float angle = Mathf.Acos(Mathf.Clamp(a.Dot(b), -1, 1)); Vector3 axis = a.Cross(b);
                if (axis.LengthSquared() < .000001f) axis = a.Cross(Math.Abs(a.Y) < .98 ? Vector3.Up : Vector3.Right);
                axis = axis.Normalized(); int count = Math.Max(2, Mathf.CeilToInt(angle / .025f));
                for (int i = 0; i < count; i++) Quad(false, a.Rotated(axis, angle * i / count) * (WorldScale.EarthRadius + .18f), a.Rotated(axis, angle * (i + 1) / count) * (WorldScale.EarthRadius + .18f), axis * .009f, color);
            }
        }
        Check(_effects!.WorldBeamSegmentCount == world && _effects.SurfaceBeamSegmentCount == surface, "world and geodesic segment counts are unchanged");
    }
    private async Task<Image> Render(bool batched)
    {
        _legacySpace.Visible = _legacySurface.Visible = !batched;
        _batchSpace.Visible = _batchSurface.Visible = batched;
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = _view.GetTexture().GetImage(); image.Convert(Image.Format.Rgba8); return image;
    }
    private void Compare(Image oldImage, Image newImage, float time)
    {
        var a = oldImage.GetData(); var b = newImage.GetData(); long difference = 0; int lit = 0;
        for (int i = 0; i < a.Length; i += 4)
        {
            if (Math.Max(Math.Max(a[i], a[i + 1]), a[i + 2]) < 24 && Math.Max(Math.Max(b[i], b[i + 1]), b[i + 2]) < 24) continue;
            lit++;
            for (int channel = 0; channel < 3; channel++) difference += Math.Abs(a[i + channel] - b[i + channel]);
        }
        double error = difference / Math.Max(1d, lit * 3d * 255);
        GD.Print($"EFFECTS_PIXEL_PARITY: t={time:0.00} lit={lit} mean_rgb_error={error:0.000000}");
        Check(lit > 300, "pixel parity renders visible real effects");
        Check(error < .02, "batched render matches original emission and animation within raster precision");
    }
    private void Benchmark()
    {
        var bursts = Bursts(120); var beams = new List<DataMap>();
        for (int i = 0; i < 100; i++) beams.AddRange(Beams().Select(row => row.DeepClone()));
        _legacySpace.Visible = _legacySurface.Visible = _batchSpace.Visible = _batchSurface.Visible = false;
        var old = new List<double>(); var batched = new List<double>();
        for (int i = 0; i < 43; i++)
        {
            foreach (var burst in bursts) burst["age"] = .2 + i * .004;
            foreach (var beam in beams) beam["life"] = .025 + i * .001;
            long start = Stopwatch.GetTimestamp(); _reference!.Sync(beams, bursts);
            double legacyMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = Stopwatch.GetTimestamp(); _effects!.Sync(beams, bursts);
            double batchMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (i >= 3) { old.Add(legacyMs); batched.Add(batchMs); }
        }
        old.Sort(); batched.Sort();
        GD.Print($"EFFECTS_NATIVE_SYNC_MS: reference_p50={old[20]:0.0000} batch_p50={batched[20]:0.0000} speedup={old[20] / Math.Max(.0001, batched[20]):0.00}x");
        Check(_effects!.BatchCount == 5 && _effects.SparkCount == 960, "120 simultaneous explosions retain 960 sparks in five total groups");
        Check(batched[20] < old[20], "batch submission is faster than equivalent original effects");
        long writes = _effects.NativeWrites, uploads = _effects.BufferUploads;
        _effects.Sync(beams, bursts);
        Check(_effects.NativeWrites == writes && _effects.BufferUploads == uploads, "unchanged effects issue no repeated native writes");
        _effects.Clear();
        writes = _effects.NativeWrites; uploads = _effects.BufferUploads;
        _effects.Sync([], []); _effects.Clear();
        Check(_effects.NativeWrites == writes && _effects.BufferUploads == uploads && _effects.BurstCount == 0 && _effects.SpareBurstCount >= 120, "empty effects reuse managed caches without native uploads or resource growth");
        int nodes = _batchSpace.GetChildCount() + _batchSurface.GetChildCount();
        _effects.Sync(Beams(), Bursts(3));
        Check(_batchSpace.GetChildCount() + _batchSurface.GetChildCount() == nodes && _effects.BatchCount == 5 && _effects.SparkCount == 24, "reappearing effects reuse existing native groups and managed spark states");
    }
    public override async void _Ready()
    {
        try
        {
            SetupScene();
            var beams = Beams(); var bursts = Bursts(9);
            foreach (float time in new[] { 0f, .15f, .5f, .9f, 1f })
            {
                foreach (var burst in bursts) burst["age"] = (double)time;
                _reference!.Sync(beams, bursts);
                long uploads = _effects!.BufferUploads;
                _effects.Sync(beams, bursts);
                Check(_effects.BufferUploads - uploads <= 5, "each effect group uploads at most one buffer per sync");
                InspectBursts(bursts); InspectBeams(beams);
                using var original = await Render(false);
                using var batch = await Render(true);
                Compare(original, batch, time);
                if (time == .5f)
                {
                    original.SavePng(ProjectSettings.GlobalizePath("res://artifacts/effects127-reference.png"));
                    batch.SavePng(ProjectSettings.GlobalizePath("res://artifacts/effects127-batched.png"));
                }
            }
            var coreMesh = (SphereMesh)_effects!.GetBatchNode("cores")!.Multimesh.Mesh;
            var shellMesh = (SphereMesh)_effects.GetBatchNode("shells")!.Multimesh.Mesh;
            var sparkMesh = (BoxMesh)_effects.GetBatchNode("sparks")!.Multimesh.Mesh;
            Check(coreMesh.RadialSegments == 16 && coreMesh.Rings == 8 && coreMesh.Radius == 1 && coreMesh.Height == 2, "core tessellation preserved");
            Check(shellMesh.RadialSegments == 32 && shellMesh.Rings == 16 && sparkMesh.Size == new Vector3(.012f, .012f, .05f), "shell tessellation and spark box dimensions preserved");
            Check(((ShaderMaterial)_effects.GetBatchNode("cores")!.MaterialOverride).GetShaderParameter("energy").AsDouble() == 10 && ((ShaderMaterial)_effects.GetBatchNode("shells")!.MaterialOverride).GetShaderParameter("energy").AsDouble() == 5.5, "original core and shell emission energies preserved");
            Benchmark();
        }
        catch (Exception error) { Check(false, error.ToString()); }
        _effects?.Clear(); _reference?.Clear();
        if (IsInstanceValid(_view)) { _view.QueueFree(); await Frames(4); }
        _effects = null; _reference = null;
        GC.Collect(); GC.WaitForPendingFinalizers();
        await Frames(3);
        GD.Print($"COMBAT_EFFECTS_BATCH_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
