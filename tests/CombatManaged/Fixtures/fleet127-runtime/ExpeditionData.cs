using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Earthward.Domain;

/// <summary>Preserved expedition investment and dormant fleet records; no disabled gameplay is re-enabled.</summary>
public sealed class ExpeditionData
{
    public static Func<DataMap, bool>? RuntimeSnapshotValidator
    {
        get; set;
    }
    public static Func<DataMap, DataMap?>? RuntimeSnapshotMigrator { get; set; }
    public static IReadOnlyList<string> SectorIds => CatalogData.Load("expedition.json").List("sector_ids").Cast<string>().ToList();
    public static DataMap DefaultSettings => CatalogData.Load("expedition.json").Map("default_settings").DeepClone();
    public static IReadOnlyList<DataMap> SettingDefinitions => CatalogData.Rows("expedition.json", "setting_definitions");
    public event Action? Changed;
    private DataMap _data = Blank();
    public bool EarthLiberated => _data.B("earth_liberated");
    public DataMap Settings => _data.Map("settings");
    public DataMap Research => _data.Map("research");
    public DataMap Sectors => _data.Map("sectors");
    public DataMap Fleet => _data.Map("fleet");
    public DataMap RuntimeSnapshot => _data.Map("runtime_snapshot").DeepClone();
    public void SetEarthLiberated(bool value)
    {
        if (value && !EarthLiberated)
        {
            _data["earth_liberated"] = true;
            Changed?.Invoke();
        }
    }
    public DataMap Serialize() => _data.DeepClone();
    public void Reset(bool emitChange = true)
    {
        _data = Blank();
        if (emitChange)
            Changed?.Invoke();
    }
    public DataMap SiloCost() => new() { ["minerals"] = Settings.N("silo_minerals_cost"), ["energy"] = Settings.N("silo_energy_cost"), ["science"] = Settings.N("silo_science_cost") };
    public bool ApplySettings(DataMap values)
    {
        var merged = Settings.DeepClone();
        foreach (var (k, v) in values)
            merged[k] = v;
        var clean = ValidateSettings(merged);
        if (clean == null)
            return false;
        _data["settings"] = clean;
        Changed?.Invoke();
        return true;
    }
    public bool SetRuntimeSnapshot(DataMap value)
    {
        var normalized = NormalizeRuntime(value);
        if (normalized == null)
            return false;
        _data["runtime_snapshot"] = normalized;
        return true;
    }
    public bool Restore(DataMap value)
    {
        var clean = Validate(value);
        if (clean == null)
            return false;
        _data = clean;
        return true;
    }
    private static DataMap Blank()
    {
        var sectors = new DataMap();
        foreach (string id in SectorIds)
            sectors[id] = new DataMap { ["observation_paid"] = false, ["status"] = "unobserved", ["revealed"] = false, ["cleared"] = false, ["rewarded_targets"] = new List<object?>() };
        return new()
        {
            ["version"] = 4L,
            ["earth_liberated"] = false,
            ["telescope_satellite"] = new DataMap { ["deployed"] = false },
            ["settings"] = DefaultSettings,
            ["research"] = new DataMap { ["telescope"] = false },
            ["legacy_research"] = new DataMap(),
            ["sectors"] = sectors,
            ["fleet"] = BlankFleet(),
            ["runtime_snapshot"] = new DataMap()
        };
    }
    public static bool SafeJson(object? value, int depth = 0)
    {
        if (depth > 32)
            return false;
        return value switch
        {
            null or bool => true,
            string s => s.Length <= 1000000,
            DataMap map => map.Count <= 1000000 && map.Values.All(v => SafeJson(v, depth + 1)),
            List<object?> list => list.Count <= 1000000 && list.All(v => SafeJson(v, depth + 1)),
            _ => DataMap.ValidNumber(value, -1e300, 1e300)
        };
    }
    private static bool ValidRuntime(DataMap value) => SafeJson(value) && (value.Count == 0 || (RuntimeSnapshotValidator?.Invoke(value) ?? (DataMap.ValidNumber(value.Value("version"), 1, 4, true) && value.Count == (value.I("version") == 1 ? 5 : 3) && value.Value("earth") is DataMap && value.Value("payload") is DataMap)));
    private static DataMap? NormalizeRuntime(DataMap value)
    {
        if (!ValidRuntime(value))
            return null;
        if (value.Count == 0 || RuntimeSnapshotMigrator == null)
            return value.DeepClone();
        var normalized = RuntimeSnapshotMigrator(value.DeepClone());
        return normalized != null && normalized.Count > 0 && ValidRuntime(normalized) ? normalized : null;
    }
    public static DataMap? ValidateSettings(DataMap values)
    {
        var result = DefaultSettings;
        if (values.Keys.Any(k => !result.ContainsKey(k)))
            return null;
        foreach (var row in SettingDefinitions)
        {
            string key = row.S("id");
            object? value = values.Value(key, row.Value("default"));
            if (!DataMap.ValidNumber(value, row.N("min"), row.N("max"), row.B("integer")))
                return null;
            result[key] = row.B("integer") ? DataMap.Integer(value) : DataMap.Number(value);
        }
        return result;
    }
    public static DataMap? Validate(object? value)
    {
        if (value is not DataMap input || !SafeJson(input))
            return null;
        if (input.Count == 0)
            return Blank();
        int version = input.I("version");
        if (!DataMap.ValidNumber(input.Value("version"), 1, 4, true) || input.Value("earth_liberated") is not bool || (version >= 3 && (input.Count != 9 || input.Value("fleet") is not DataMap || input.Value("legacy_research") is not DataMap)) || input.Value("settings") is not DataMap sourceSettings)
            return null;
        var settingsInput = sourceSettings.DeepClone();
        if (version < 3)
            foreach (string key in settingsInput.Keys.Where(k => !DefaultSettings.ContainsKey(k) || k.StartsWith("carrier_", StringComparison.Ordinal)).ToList())
                settingsInput.Remove(key);
        var settings = ValidateSettings(settingsInput);
        if (settings == null)
            return null;
        bool shrink = version == 3 && settings.N("carrier_scale_multiplier") == 25;
        if (shrink)
            settings["carrier_scale_multiplier"] = 8d;
        if (input.Value("research") is not DataMap research || research.Value("telescope") is not bool)
            return null;
        var legacy = input.Map("legacy_research").DeepClone();
        var ids = CatalogData.Load("expedition.json").List("legacy_tech_ids");
        foreach (var (key, v) in research)
        {
            if (key == "telescope")
                continue;
            if (version >= 3 || !ids.Contains(key) || !DataMap.ValidNumber(v, 0, 5, true))
                return null;
            legacy[key] = DataMap.Integer(v);
        }
        if (legacy.Any(p => !ids.Contains(p.Key) || !DataMap.ValidNumber(p.Value, 0, 5, true)) || (research.B("telescope") && !input.B("earth_liberated")))
            return null;
        var satellite = new DataMap { ["deployed"] = research.B("telescope") };
        if (version >= 2)
        {
            if (input.Value("telescope_satellite") is not DataMap sat || sat.Count != 1 || sat.Value("deployed") is not bool)
                return null;
            satellite = sat.DeepClone();
        }
        if (satellite.B("deployed") && !research.B("telescope"))
            return null;
        if (input.Value("sectors") is not DataMap sectors || sectors.Count != SectorIds.Count)
            return null;
        var cleaned = new DataMap();
        foreach (string id in SectorIds)
        {
            if (sectors.Value(id) is not DataMap sector || new[] { "observation_paid", "revealed", "cleared" }.Any(k => sector.Value(k) is not bool))
                return null;
            if (sector.B("observation_paid") && (!research.B("telescope") || !satellite.B("deployed")) || sector.B("cleared") && !sector.B("revealed"))
                return null;
            var claimed = sector.ContainsKey("rewarded_targets") ? sector.List("rewarded_targets") : new List<object?>();
            if (claimed.Count > 2 || claimed.Distinct().Count() != claimed.Count || claimed.Any(v => v is not string kind || kind is not ("hive" or "barracks") || !sector.B("revealed")))
                return null;
            bool revealed = version < 3 ? sector.B("observation_paid") : sector.B("revealed");
            if (version >= 3 && (revealed != sector.B("observation_paid") || sector.Count != 5))
                return null;
            string status = sector.B("cleared") ? "cleared" : revealed ? "revealed" : "unobserved";
            if (version >= 3 && sector.S("status") != status)
                return null;
            cleaned[id] = new DataMap { ["observation_paid"] = sector.B("observation_paid"), ["status"] = status, ["revealed"] = revealed, ["cleared"] = sector.B("cleared"), ["rewarded_targets"] = DataMap.Clone(claimed) };
        }
        var fleet = ValidateFleet(version >= 3 ? input.Map("fleet") : new());
        if (fleet == null)
            return null;
        if (shrink)
            ShrinkCarriers(fleet, 8f / 25f);
        if (!research.B("telescope") && (fleet.List("orders").Count > 0 || fleet.List("ships").Count > 0))
            return null;
        if (input.Value("runtime_snapshot") is not DataMap runtime)
            return null;
        var normalizedRuntime = NormalizeRuntime(runtime);
        if (normalizedRuntime == null)
            return null;
        return new()
        {
            ["version"] = 4L,
            ["earth_liberated"] = input.B("earth_liberated"),
            ["telescope_satellite"] = satellite,
            ["settings"] = settings,
            ["research"] = new DataMap { ["telescope"] = research.B("telescope") },
            ["legacy_research"] = legacy,
            ["sectors"] = cleaned,
            ["fleet"] = fleet,
            ["runtime_snapshot"] = normalizedRuntime
        };
    }
    private static DataMap BlankFleet() => new() { ["version"] = 4L, ["next_id"] = 3000001L, ["orders"] = new List<object?>(), ["ships"] = new List<object?>() };
    private static bool Vector(object? value) => value is List<object?> list && list.Count == 3 && list.All(v => DataMap.ValidNumber(v, -1e8 - WorldScale.EarthRadiusDelta, 1e8 + WorldScale.EarthRadiusDelta));
    public static DataMap? ValidateFleet(DataMap value)
    {
        if (value.Count == 0)
            return BlankFleet();
        if (value.Count != 4 || !DataMap.ValidNumber(value.Value("version"), 1, 4, true) || !DataMap.ValidNumber(value.Value("next_id"), 3000001, 9007199254740991, true) || value.Value("orders") is not List<object?> orders || value.Value("ships") is not List<object?> ships)
            return null;
        double sourceRadius = DefenseState.RadiusForWorldScaleVersion(Math.Max(1, value.I("version") - 1));
        double radiusDelta = WorldScale.EarthRadius - sourceRadius;
        var used = new HashSet<long>();
        var active = new HashSet<long>();
        var slots = new HashSet<long>();
        foreach (var v in orders)
        {
            if (v is not DataMap row || row.Count != 8 || !DataMap.ValidNumber(row.Value("id"), 3000001, value.N("next_id") - 1, true) || !used.Add(row.L("id")) || row.S("kind") is not ("destroyer" or "carrier") || !DataMap.ValidNumber(row.Value("silo_id"), 0, 1000000, true) || !DataMap.ValidNumber(row.Value("duration"), 1, 3600) || !DataMap.ValidNumber(row.Value("elapsed"), 0, row.N("duration")) || !DataMap.ValidNumber(row.Value("launch_seconds"), 1, 60) || !DataMap.ValidNumber(row.Value("transit_seconds"), 1, 300) || (active.Contains(row.L("silo_id")) && row.N("elapsed") > 0) || row.Value("cost") is not DataMap cost || cost.Count != 3 || new[] { "minerals", "energy", "science" }.Any(k => !DataMap.ValidNumber(cost.Value(k), 0, 1e9)))
                return null;
            active.Add(row.L("silo_id"));
        }
        foreach (var v in ships)
        {
            if (v is not DataMap row || row.Count != 18 || !DataMap.ValidNumber(row.Value("uid"), 3000001, value.N("next_id") - 1, true) || !used.Add(row.L("uid")) || row.S("kind") is not ("destroyer" or "carrier") || row.S("state") is not ("launching" or "transit" or "parked") || !DataMap.ValidNumber(row.Value("silo_id"), 0, 1000000, true) || !DataMap.ValidNumber(row.Value("slot"), 0, 1000000, true) || !slots.Add(row.L("slot")) || !DataMap.ValidNumber(row.Value("age"), 0, 1e12) || !DataMap.ValidNumber(row.Value("launch_seconds"), 1, 60) || !DataMap.ValidNumber(row.Value("transit_seconds"), 1, 300) || !DataMap.ValidNumber(row.Value("clearance"), sourceRadius, 1000 + sourceRadius - WorldScale.LegacyEarthRadius) || !DataMap.ValidNumber(row.Value("hull_length"), .01, 1000))
                return null;
            if (new[] { "launch_origin", "launch_end", "launch_normal", "launch_up", "target", "position", "forward", "up" }.Any(key => !Vector(row.Value(key))) || new[] { "launch_normal", "launch_up", "forward", "up" }.Any(key => Math.Abs(row.Vector3(key).Length() - 1) > .005))
                return null;
            string expected = row.N("age") < row.N("launch_seconds") ? "launching" : row.N("age") < row.N("launch_seconds") + row.N("transit_seconds") ? "transit" : "parked";
            if (row.S("state") != expected)
                return null;
        }
        var result = value.DeepClone();
        result["version"] = 4L;
        if (value.I("version") == 1)
            foreach (var ship in result.List("ships").OfType<DataMap>())
            {
                ship["target"] = Vec(StagingTarget(ship.I("slot")));
                RelocateLegacy(ship);
            }
        if (radiusDelta != 0)
            foreach (var ship in result.List("ships").OfType<DataMap>())
            {
                foreach (string key in new[] { "launch_origin", "launch_end", "position", "target" })
                    ship[key] = ExpandLegacyPosition(ship.List(key), radiusDelta);
                ship["clearance"] = ship.N("clearance") + radiusDelta;
            }
        return result;
    }
    private static List<object?> ExpandLegacyPosition(List<object?> position, double radiusDelta)
    {
        double x = DataMap.Number(position[0]), y = DataMap.Number(position[1]), z = DataMap.Number(position[2]);
        double radius = Math.Sqrt(x * x + y * y + z * z);
        if (radius <= 1e-12)
            return (List<object?>)DataMap.Clone(position)!;
        double scale = 1 + radiusDelta / radius;
        return new() { x * scale, y * scale, z * scale };
    }
    private static List<object?> Vec(Vector3 v) => new() { (double)v.X, (double)v.Y, (double)v.Z };
    private static Vector3 StagingTarget(int slot)
    {
        int row = slot / 2 % 4, column = slot % 2, band = slot / 8;
        return new Vector3(column == 0 ? -5.1f : -2.7f, 1.425f - row * .95f, column == 0 ? 3.8f : 6.7f) + new Vector3(-1.6f * band, 0, -2.4f * band);
    }
    private static float Smooth(float low, float high, float value)
    {
        float x = Math.Clamp((value - low) / (high - low), 0, 1);
        return x * x * (3 - 2 * x);
    }
    private static void RelocateLegacy(DataMap ship)
    {
        if (ship.S("state") == "launching")
            return;
        var target = ship.Vector3("target");
        float minimum = ship.Vector3("launch_origin").Length() + .18f;
        if (ship.S("state") == "parked")
        {
            ship["position"] = Vec(target.Normalized() * Math.Max(minimum, target.Length()));
            return;
        }
        float progress = (float)Math.Clamp((ship.N("age") - ship.N("launch_seconds")) / ship.N("transit_seconds"), 0, 1), turn = Smooth(0, 1, Math.Min(1, progress / .72f));
        var a = ship.Vector3("launch_normal");
        var b = target.Normalized();
        var radial = a.Dot(b) < -.999f ? a.Rotated(a.Cross(Math.Abs(a.Y) < .9 ? Vector3.Up : Vector3.Right).Normalized(), MathF.PI * turn) : a.Slerp(b, turn).Normalized();
        float radius = (float)ship.N("clearance") + (Math.Max(minimum, target.Length()) - (float)ship.N("clearance")) * Smooth(.55f, 1, progress);
        ship["position"] = Vec(radial * radius);
    }
    private static void ShrinkCarriers(DataMap fleet, float ratio)
    {
        foreach (var ship in fleet.List("ships").OfType<DataMap>())
        {
            if (ship.S("kind") != "carrier")
                continue;
            float old = (float)ship.N("hull_length");
            var normal = ship.Vector3("launch_normal");
            float surface = ship.Vector3("launch_origin").Dot(normal) - old * .5f - .22f, length = old * ratio, clearance = Math.Max(surface + length * .5f + 1.4f, 6.5f + WorldScale.EarthRadiusDelta);
            ship["hull_length"] = (double)length;
            ship["clearance"] = (double)clearance;
            ship["launch_origin"] = Vec(normal * (surface + length * .5f + .22f));
            ship["launch_end"] = Vec(normal * clearance);
            if (ship.S("state") == "launching")
                ship["position"] = Vec(ship.Vector3("launch_origin").Lerp(ship.Vector3("launch_end"), Smooth(0, 1, (float)(ship.N("age") / ship.N("launch_seconds")))));
            else if (ship.S("state") == "transit")
            {
                float t = (float)Math.Clamp((ship.N("age") - ship.N("launch_seconds")) / ship.N("transit_seconds"), 0, 1), target = Math.Max(ship.Vector3("launch_origin").Length() + .18f, ship.Vector3("target").Length());
                ship["position"] = Vec(ship.Vector3("position").Normalized() * (clearance + (target - clearance) * Smooth(.55f, 1, t)));
            }
        }
    }
}
