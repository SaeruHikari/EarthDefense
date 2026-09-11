using Godot;
using System.Text.Json;
using Earthward.Domain;
namespace Earthward.Rendering;

/// <summary>Immutable baked content; no scripts are loaded from the legacy tree.</summary>
public static class RenderAssets
{
    private const string ModelDirectory = "res://assets/managed/models/";
    private static readonly Dictionary<string, PackedScene> Scenes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> ModelFailures = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> ModelPaths = new[]
    {
        "airframe/K1", "airframe/K2", "airframe/K3",
        "airframe/M1", "airframe/M2", "airframe/M3",
        "airframe/L1", "airframe/L2", "airframe/L3",
        "enemy/claw", "enemy/needle", "enemy/rock", "enemy/siege",
        "enemy/prism", "enemy/weaver", "enemy/hatcher", "enemy/jammer",
        "boss/brood", "boss/forge", "boss/prism",
        "meteor", "scout", "cruiser", "small_boss", "boss", "carrier", "mothership",
        "projectile/interceptor", "projectile/laser", "projectile/missile", "projectile/hostile"
    }.ToDictionary(key => key, key => ModelDirectory + key.Replace('/', '_') + ".scn", StringComparer.Ordinal);

    public static IReadOnlyCollection<string> ModelKeys => ModelPaths.Keys;
    public static IReadOnlyDictionary<string, string> ModelFallbackDiagnostics => ModelFailures;
    internal static bool IsSceneCached(string path) => Scenes.ContainsKey(path);

    public static string CanonicalModelKey(string key) => key switch
    {
        // Older combat packets called a kinetic projectile "friendly" (its side),
        // while the baked asset is named after the interceptor weapon family.
        "projectile/friendly" or "friendly" => "projectile/interceptor",
        "hostile" => "projectile/hostile",
        "interceptor" => "airframe/K1",
        "laser" => "airframe/L1",
        "missile" => "airframe/M1",
        _ => key
    };

    public static string ModelPath(string key)
    {
        string canonical = CanonicalModelKey(key);
        return ModelPaths.GetValueOrDefault(canonical, "");
    }

    public static Node3D Instantiate(string path)
    {
        if (TryInstantiate(path, out var instance, out string reason))
            return instance!;
        throw new InvalidDataException($"Required scene could not be instantiated: '{path}'. {reason}");
    }

    private static bool TryInstantiate(string path, out Node3D? instance, out string reason)
    {
        instance = null;
        reason = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "No resource path is registered.";
            return false;
        }
        if (!Scenes.TryGetValue(path, out var scene) || !GodotObject.IsInstanceValid(scene))
        {
            // Never poison the positive cache with a null GD.Load result. A missing
            // asset can be restored during development and loaded on the next request.
            Scenes.Remove(path);
            if (!ResourceLoader.Exists(path, "PackedScene"))
            {
                reason = "PackedScene resource does not exist.";
                return false;
            }
            try
            {
                scene = GD.Load<PackedScene>(path);
            }
            catch (Exception error)
            {
                reason = error.GetType().Name + ": " + error.Message;
                return false;
            }
            if (scene == null || !scene.CanInstantiate())
            {
                reason = "The resource is not an instantiable PackedScene.";
                return false;
            }
        }
        try
        {
            var root = scene.Instantiate();
            if (root is not Node3D spatial)
            {
                string actual = root?.GetClass().ToString() ?? "null";
                root?.Free();
                Scenes.Remove(path);
                reason = "Expected a Node3D root, got " + actual + ".";
                return false;
            }
            instance = spatial;
            Scenes[path] = scene;
            return true;
        }
        catch (Exception error)
        {
            Scenes.Remove(path);
            reason = error.GetType().Name + ": " + error.Message;
            return false;
        }
    }

    public static bool HasRenderableGeometry(Node3D root) =>
        Meshes(root).Any(part => part.Visible && part.Mesh != null && part.Mesh.GetSurfaceCount() > 0);

    private static bool TryModel(string key, out Node3D? model, out string reason)
    {
        if (!TryInstantiate(ModelPath(key), out model, out reason))
            return false;
        if (HasRenderableGeometry(model!))
            return true;
        model!.Free();
        model = null;
        reason = "The model scene has no visible nonempty mesh surfaces.";
        return false;
    }

    public static Node3D Model(string key)
    {
        string canonical = CanonicalModelKey(key);
        if (TryModel(canonical, out var model, out string reason))
            return model!;

        string fallbackKey = FallbackKey(canonical);
        if (fallbackKey != canonical && TryModel(fallbackKey, out var replacement, out _))
        {
            ReportModelFallback(canonical, reason, fallbackKey);
            replacement!.SetMeta("model_fallback", canonical);
            return replacement;
        }
        ReportModelFallback(canonical, reason, "procedural " + fallbackKey);
        return CreateFallbackModel(canonical);
    }

    private static string FallbackKey(string key)
    {
        if (key.StartsWith("projectile/", StringComparison.Ordinal))
            return key.Contains("hostile", StringComparison.Ordinal) ? "projectile/hostile" :
                key.Contains("missile", StringComparison.Ordinal) ? "projectile/missile" :
                key.Contains("laser", StringComparison.Ordinal) ? "projectile/laser" : "projectile/interceptor";
        if (key.StartsWith("airframe/", StringComparison.Ordinal))
            return key.StartsWith("airframe/M", StringComparison.Ordinal) ? "airframe/M1" :
                key.StartsWith("airframe/L", StringComparison.Ordinal) ? "airframe/L1" : "airframe/K1";
        return key.StartsWith("boss/", StringComparison.Ordinal) || key is "boss" or "small_boss" or "carrier" or "mothership"
            ? "boss/brood" : "enemy/claw";
    }

    internal static void ReportModelFallback(string key, string reason, string fallback)
    {
        if (!ModelFailures.TryAdd(key, reason))
            return;
        string requested = ModelPath(key);
        GD.PushWarning($"MODEL_ASSET_FALLBACK: '{key}' ({(requested.Length > 0 ? requested : "unregistered model key")}) -> '{fallback}'. {reason}");
    }

    internal static Node3D CreateFallbackModel(string key)
    {
        var root = new Node3D { Name = "VisibleCombatFallback" };
        root.SetMeta("model_fallback", key);
        bool projectile = key.StartsWith("projectile/", StringComparison.Ordinal);
        bool friendly = key.StartsWith("airframe/", StringComparison.Ordinal);
        bool missile = key.Contains("missile", StringComparison.Ordinal) || key.StartsWith("airframe/M", StringComparison.Ordinal);
        bool laser = key.Contains("laser", StringComparison.Ordinal) || key.StartsWith("airframe/L", StringComparison.Ordinal);
        Color color = key.Contains("hostile", StringComparison.Ordinal) ? new("ef958b") :
            missile ? new("eabe7e") : laser ? new("79c8dd") : friendly || projectile ? new("9be4cc") : new("b599de");
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = projectile ? 0 : .65f,
            Roughness = projectile ? 1 : .36f,
            ShadingMode = projectile ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            EmissionEnabled = !projectile,
            Emission = color * .1f
        };
        void Part(Mesh mesh, Vector3 position, Vector3 rotation = default) => root.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            Rotation = rotation,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        if (projectile)
        {
            Part(new BoxMesh { Size = new(.018f, .018f, missile ? .072f : .050f) }, Vector3.Zero);
            if (missile)
                Part(new BoxMesh { Size = new(.032f, .008f, .014f) }, new(0, 0, .018f));
            return root;
        }
        if (friendly)
        {
            Part(new BoxMesh { Size = new(.055f, .025f, .20f) }, Vector3.Zero);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(new PrismMesh { Size = new(.08f, .015f, .13f) }, new(side * .055f, 0, .025f), new(0, side * .30f, 0));
                Part(new BoxMesh { Size = new(.014f, .014f, laser ? .15f : .06f) }, new(side * .031f, .018f, -.07f));
            }
        }
        else
        {
            // An alien central organ and three swept prongs stay distinct from the
            // two-wing human silhouette even if every baked model is unavailable.
            Part(new SphereMesh { Radius = .055f, Height = .14f, RadialSegments = 12, Rings = 6 }, Vector3.Zero, new(Mathf.Pi * .5f, 0, 0));
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.Tau / 3;
                Part(new PrismMesh { Size = new(.035f, .025f, .19f) }, new(Mathf.Cos(angle) * .058f, Mathf.Sin(angle) * .058f, -.005f), new(0, 0, angle));
            }
        }
        return root;
    }
    public static DataMap ReadMap(string path)
    {
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        return (DataMap)Decode(json.RootElement)!;
    }
    public static List<object?> ReadList(string path)
    {
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        return (List<object?>)Decode(json.RootElement)!;
    }
    private static object? Decode(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return e.GetString();
            case JsonValueKind.Number:
                return e.TryGetInt64(out long i) ? (object)i : e.GetDouble();
            case JsonValueKind.Array:
                return e.EnumerateArray().Select(Decode).ToList();
            case JsonValueKind.Object:
                if (e.TryGetProperty("@", out var tag))
                {
                    var v = e.GetProperty("v");
                    switch (tag.GetString())
                    {
                        case "int":
                            return long.Parse(v[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        case "v3":
                            return new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle());
                        case "color":
                            return new Color(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle(), v[3].GetSingle());
                        case "basis":
                            return new Basis((Vector3)Decode(v[0])!, (Vector3)Decode(v[1])!, (Vector3)Decode(v[2])!);
                        case "map":
                            var map = new DataMap();
                            foreach (var pair in v.EnumerateArray())
                                map[Convert.ToString(Decode(pair[0]), System.Globalization.CultureInfo.InvariantCulture)!] = Decode(pair[1]);
                            return map;
                    }
                }
                var plain = new DataMap();
                foreach (var p in e.EnumerateObject())
                    plain[p.Name] = Decode(p.Value);
                return plain;
        }
        throw new InvalidDataException("Unsupported baked data value");
    }
    public static IEnumerable<MeshInstance3D> Meshes(Node root)
    {
        if (root is MeshInstance3D mesh)
            yield return mesh;
        foreach (var child in root.GetChildren())
            foreach (var part in Meshes(child))
                yield return part;
    }
    public static ShaderMaterial ShaderMaterial(MeshInstance3D mesh) => (ShaderMaterial)(mesh.MaterialOverride ?? mesh.Mesh.SurfaceGetMaterial(0));
}
