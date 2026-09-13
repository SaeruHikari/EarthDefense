using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

/// <summary>Six reusable PBR pickup batches. Simulation positions and pickup state remain owned by the application.</summary>
public sealed class LootRenderer
{
    public const float BaseWorldScale = .4f;
    private const float BobAmplitude = .035f;
    private static readonly string[] Currencies = ["minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips"];
    private static readonly Dictionary<string, Prototype> Prototypes = new(StringComparer.Ordinal);
    private sealed record Prototype(Mesh Mesh, Transform3D Local, float Radius, float Diameter, int Triangles);

    private sealed class Bucket
    {
        public required string Currency;
        public required Prototype Prototype;
        public required MultiMeshInstance3D Node;
        public float[] Buffer = [];
        public int Count, Capacity, NativeCapacity, LastCount;
        public bool Visible;
        public Vector3 Minimum, Maximum;
        public Aabb LastBounds;

        public void Begin()
        {
            Count = 0;
            Minimum = Vector3.One * float.PositiveInfinity;
            Maximum = Vector3.One * float.NegativeInfinity;
        }

        public void Add(in Transform3D transform, float radius)
        {
            if (Count == Capacity)
            {
                Capacity = Math.Max(16, Capacity * 2);
                Array.Resize(ref Buffer, Capacity * 12);
            }
            // Same row-major 3x4 packing as FleetRenderer. No native call per pickup.
            Span<float> row = Buffer.AsSpan(Count++ * 12, 12);
            var basis = transform.Basis;
            row[0] = basis.X.X; row[1] = basis.Y.X; row[2] = basis.Z.X; row[3] = transform.Origin.X;
            row[4] = basis.X.Y; row[5] = basis.Y.Y; row[6] = basis.Z.Y; row[7] = transform.Origin.Y;
            row[8] = basis.X.Z; row[9] = basis.Y.Z; row[10] = basis.Z.Z; row[11] = transform.Origin.Z;
            var extent = Vector3.One * radius;
            Minimum = Minimum.Min(transform.Origin - extent);
            Maximum = Maximum.Max(transform.Origin + extent);
        }

        public void Upload()
        {
            var multiMesh = Node.Multimesh;
            if (Count == 0)
            {
                if (LastCount != 0) multiMesh.VisibleInstanceCount = 0;
                if (Visible) Node.Visible = false;
                LastCount = 0;
                Visible = false;
                return;
            }
            if (NativeCapacity != Capacity)
            {
                multiMesh.InstanceCount = Capacity;
                NativeCapacity = Capacity;
            }
            var bounds = new Aabb(Minimum, Maximum - Minimum);
            if (bounds != LastBounds)
            {
                multiMesh.CustomAabb = bounds;
                LastBounds = bounds;
            }
            multiMesh.Buffer = Buffer;
            if (LastCount != Count) multiMesh.VisibleInstanceCount = Count;
            if (!Visible) Node.Visible = true;
            LastCount = Count;
            Visible = true;
        }
    }

    private readonly Node3D _root;
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    public int InstanceCount { get; private set; }
    public int BatchCount => _buckets.Count;

    public LootRenderer(Node3D spaceRoot)
    {
        // Validate the complete library before attaching any nodes. A missing
        // asset must not leave half-created batches in SpaceRoot.
        var prototypes = Currencies.Select(GetPrototype).ToArray();
        _root = new Node3D { Name = "WorldLootPickups" };
        spaceRoot.AddChild(_root);
        for (int i = 0; i < Currencies.Length; i++)
        {
            string currency = Currencies[i];
            var prototype = prototypes[i];
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = prototype.Mesh,
                UseColors = false,
                UseCustomData = false
            };
            var node = new MultiMeshInstance3D
            {
                Name = "Loot_" + currency,
                Multimesh = multiMesh,
                // The cloned mesh retains all four authored materials; a blanket
                // MaterialOverride would erase the metal / core separation.
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false
            };
            _root.AddChild(node);
            var bucket = new Bucket { Currency = currency, Prototype = prototype, Node = node };
            bucket.Begin();
            _buckets.Add(currency, bucket);
        }
    }

    public void Sync(IReadOnlyList<DataMap> pickups, Camera3D camera, Vector2I viewSize, double uiTime, long hoveredUid)
    {
        foreach (var bucket in _buckets.Values) bucket.Begin();
        InstanceCount = 0;
        if (pickups.Count == 0)
        {
            foreach (var bucket in _buckets.Values) bucket.Upload();
            return;
        }

        // Capture Godot properties once; the per-item loop only touches managed
        // math, dictionaries and the reusable transform buffers.
        Transform3D cameraTransform = camera.GetCameraTransform();
        Transform3D view = cameraTransform.AffineInverse();
        Transform3D rootInverse = _root.GlobalTransform.AffineInverse();
        Basis facing = cameraTransform.Basis.Orthonormalized();
        Projection projection = camera.GetCameraProjection();
        float focalPixels = Math.Abs(projection.Y.Y) * Math.Max(1, viewSize.Y) * .5f;
        float near = camera.Near;
        float time = (float)(double.IsFinite(uiTime) ? uiTime % 36000 : 0);
        float rootScale = Math.Max(rootInverse.Basis.X.Length(), Math.Max(rootInverse.Basis.Y.Length(), rootInverse.Basis.Z.Length()));

        foreach (var pickup in pickups)
        {
            // Flying pickups are represented by the HUD animation exclusively.
            if (pickup.S("phase") != "world") continue;
            double amount = pickup.N("amount");
            if (!double.IsFinite(amount) || amount <= 0) continue;
            string currency = pickup.S("currency");
            if (!_buckets.TryGetValue(currency, out var bucket)) continue;
            Vector3 position = pickup.Vector3("space_position", new(float.NaN, float.NaN, float.NaN));
            if (!position.IsFinite()) continue;
            Vector3 cameraLocal = view * position;
            if (cameraLocal.Z >= -near) continue;
            Vector4 clip = projection * new Vector4(cameraLocal.X, cameraLocal.Y, cameraLocal.Z, 1);
            if (!float.IsFinite(clip.W) || clip.W <= .0001f) continue;

            long uid = pickup.L("uid", -1);
            float phase = (float)(uid % 997) * 2.399963f;
            bool hovered = uid == hoveredUid && hoveredUid >= 0;
            float scale = ComputeScale(focalPixels / clip.W, bucket.Prototype.Diameter, hovered);
            bool flat = currency is "alien_points" or "alien_chips";
            // Flat rare pickups turn gently toward the camera, preserving their
            // distinctive triangle and chip silhouettes throughout the rotation.
            float yaw = flat ? .30f * Mathf.Sin(time * .32f + phase) : time * .30f + phase;
            float roll = (flat ? .10f : .15f) * Mathf.Sin(time * .27f + phase);
            Basis rotation = facing * Basis.FromEuler(new Vector3(-.24f, yaw, roll));
            Vector3 bob = facing.Y * (BobAmplitude * Mathf.Sin(time * 1.4f + phase));
            var world = new Transform3D(rotation.Scaled(Vector3.One * scale), position + bob);
            var local = rootInverse * world * bucket.Prototype.Local;
            bucket.Add(local, bucket.Prototype.Radius * scale * rootScale + .002f);
            InstanceCount++;
        }

        foreach (var bucket in _buckets.Values) bucket.Upload();
    }

    /// <summary>Keep the asset's longest dimension near 16-24 logical pixels while preserving the .4 default scale.</summary>
    private static float ComputeScale(float pixelsPerWorldUnit, float diameter, bool hovered)
    {
        float baseline = BaseWorldScale * (hovered ? 1.10f : 1);
        float projected = Math.Max(.00001f, diameter * baseline * pixelsPerWorldUnit);
        float desired = Mathf.Clamp(projected, hovered ? 20 : 16, hovered ? 26 : 24);
        return baseline * desired / projected;
    }

    public void Clear()
    {
        InstanceCount = 0;
        foreach (var bucket in _buckets.Values)
        {
            bucket.Begin();
            bucket.Upload();
        }
    }

    public IReadOnlyList<DataMap> Diagnostics() => _buckets.Values.Select(bucket => new DataMap
    {
        ["currency"] = bucket.Currency,
        ["instances"] = bucket.Count,
        ["capacity"] = bucket.Capacity,
        ["surfaces"] = bucket.Prototype.Mesh.GetSurfaceCount(),
        ["triangles"] = bucket.Prototype.Triangles,
        ["radius"] = bucket.Prototype.Radius,
        ["casts_shadow"] = false
    }).ToArray();

    /// <summary>Runtime probe for the exact imported or raw GLBs consumed by the renderer.</summary>
    public static IReadOnlyList<DataMap> ValidateAssets()
    {
        var results = new List<DataMap>(Currencies.Length);
        foreach (string currency in Currencies)
        {
            try
            {
                var prototype = GetPrototype(currency);
                var bounds = prototype.Local * prototype.Mesh.GetAabb();
                results.Add(new DataMap
                {
                    ["currency"] = currency,
                    ["ok"] = prototype.Mesh.GetSurfaceCount() == 4 && prototype.Triangles < 1000 && prototype.Radius <= .4801f,
                    ["surfaces"] = prototype.Mesh.GetSurfaceCount(),
                    ["triangles"] = prototype.Triangles,
                    ["radius"] = prototype.Radius,
                    ["bounds_min"] = bounds.Position,
                    ["bounds_size"] = bounds.Size
                });
            }
            catch (Exception error)
            {
                results.Add(new DataMap { ["currency"] = currency, ["ok"] = false, ["error"] = error.Message });
            }
        }
        return results;
    }

    private static Prototype GetPrototype(string currency)
    {
        if (Prototypes.TryGetValue(currency, out var cached) && GodotObject.IsInstanceValid(cached.Mesh)) return cached;
        string path = "res://assets/models/loot/loot_" + currency + ".glb";
        Node3D model = LoadModel(path);
        try
        {
            var parts = new List<(MeshInstance3D Node, Transform3D Local)>();
            Collect(model, Transform3D.Identity, parts);
            if (parts.Count != 1)
                throw new InvalidDataException($"Loot asset must contain one mesh: {path} ({parts.Count} found).");
            var (source, local) = parts[0];
            var mesh = (Mesh)source.Mesh.Duplicate();
            for (int i = 0; i < mesh.GetSurfaceCount(); i++)
            {
                Material? material = source.GetActiveMaterial(i);
                if (material == null) throw new InvalidDataException($"Loot asset has an unmaterialed surface: {path} [{i}].");
                mesh.SurfaceSetMaterial(i, (Material)material.Duplicate());
            }
            Vector3[] faces = mesh.GetFaces();
            float radius = 0;
            foreach (Vector3 vertex in faces)
            {
                Vector3 point = local * vertex;
                if (!point.IsFinite()) throw new InvalidDataException("Loot asset has a non-finite vertex: " + path);
                radius = Math.Max(radius, point.Length());
            }
            var bounds = local * mesh.GetAabb();
            float diameter = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
            if (mesh.GetSurfaceCount() != 4 || radius <= .01f || radius > .4801f || faces.Length / 3 >= 1000)
                throw new InvalidDataException("Loot asset violates the four-material, normalized low-poly contract: " + path);
            return Prototypes[currency] = new(mesh, local, radius, diameter, faces.Length / 3);
        }
        finally
        {
            model.Free();
        }
    }

    private static Node3D LoadModel(string path)
    {
        // Normal Godot imports / exported packs retain their PackedScene remap.
        if (ResourceLoader.Exists(path, "PackedScene"))
        {
            var scene = GD.Load<PackedScene>(path);
            if (scene != null && scene.CanInstantiate()) return scene.Instantiate<Node3D>();
        }
        // Development and validation can also consume the original Blender GLB
        // before the editor has generated its .import sidecar.
        using var document = new GltfDocument();
        using var state = new GltfState();
        Error result = document.AppendFromFile(path, state);
        if (result != Error.Ok) throw new InvalidDataException($"Could not load loot GLB '{path}': {result}.");
        Node generated = document.GenerateScene(state);
        if (generated is Node3D model) return model;
        generated?.Free();
        throw new InvalidDataException("Loot GLB did not generate a Node3D scene: " + path);
    }

    private static void Collect(Node3D node, Transform3D parent, List<(MeshInstance3D Node, Transform3D Local)> parts)
    {
        Transform3D local = parent * node.Transform;
        if (node is MeshInstance3D mesh && mesh.Mesh != null && mesh.Mesh.GetSurfaceCount() > 0)
            parts.Add((mesh, local));
        foreach (var child in node.GetChildren())
            if (child is Node3D spatial) Collect(spatial, local, parts);
    }
}
