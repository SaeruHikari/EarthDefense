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
        if (!DataMap.ValidNumber(input.Value("version"), 4, 4, true) || input.Count != 8 || input.Value("earth_liberated") is not bool || input.Value("fleet") is not DataMap || input.Value("settings") is not DataMap sourceSettings)
            return null;
        var settings = ValidateSettings(sourceSettings.DeepClone());
        if (settings == null)
            return null;
        if (input.Value("research") is not DataMap research || research.Count != 1 || research.Value("telescope") is not bool)
            return null;
        if (input.Value("telescope_satellite") is not DataMap satellite || satellite.Count != 1 || satellite.Value("deployed") is not bool)
            return null;
        if (satellite.B("deployed") != research.B("telescope") || research.B("telescope") && !input.B("earth_liberated"))
            return null;
        if (input.Value("sectors") is not DataMap sectors || sectors.Count != SectorIds.Count)
            return null;
        var cleaned = new DataMap();
        foreach (string id in SectorIds)
        {
            if (sectors.Value(id) is not DataMap sector || sector.Count != 5 || new[] { "observation_paid", "revealed", "cleared" }.Any(k => sector.Value(k) is not bool))
                return null;
            if (sector.B("observation_paid") && (!research.B("telescope") || !satellite.B("deployed")) || sector.B("cleared") && !sector.B("revealed") || sector.B("observation_paid") != sector.B("revealed"))
                return null;
            var claimed = sector.ContainsKey("rewarded_targets") ? sector.List("rewarded_targets") : new List<object?>();
            if (claimed.Count > 2 || claimed.Distinct().Count() != claimed.Count || claimed.Any(v => v is not string kind || kind is not ("hive" or "barracks") || !sector.B("revealed")))
                return null;
            string status = sector.B("cleared") ? "cleared" : sector.B("revealed") ? "revealed" : "unobserved";
            if (sector.S("status") != status)
                return null;
            cleaned[id] = new DataMap { ["observation_paid"] = sector.B("observation_paid"), ["status"] = status, ["revealed"] = sector.B("revealed"), ["cleared"] = sector.B("cleared"), ["rewarded_targets"] = DataMap.Clone(claimed) };
        }
        var fleet = ValidateFleet(input.Map("fleet"));
        if (fleet == null)
            return null;
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
            ["telescope_satellite"] = satellite.DeepClone(),
            ["settings"] = settings,
            ["research"] = new DataMap { ["telescope"] = research.B("telescope") },
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
        if (value.Count != 4 || !DataMap.ValidNumber(value.Value("version"), 4, 4, true) || !DataMap.ValidNumber(value.Value("next_id"), 3000001, 9007199254740991, true) || value.Value("orders") is not List<object?> orders || value.Value("ships") is not List<object?> ships)
            return null;
        double sourceRadius = WorldScale.EarthRadius;
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
        return value.DeepClone();
    }
}
