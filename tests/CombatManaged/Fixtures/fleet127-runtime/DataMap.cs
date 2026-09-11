using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Earthward.Domain;

/// <summary>Owned managed JSON tree. No Godot Dictionary/Variant crosses domain boundaries.</summary>
public sealed class DataMap : Dictionary<string, object?>
{
    public DataMap() : base(StringComparer.Ordinal) { }
    public DataMap(IEnumerable<KeyValuePair<string, object?>> entries) : base(entries, StringComparer.Ordinal) { }
    public object? Value(string key, object? fallback = null) => TryGetValue(key, out var value) ? value : fallback;
    public double N(string key, double fallback = 0) => Number(Value(key), fallback);
    public long L(string key, long fallback = 0) => Integer(Value(key), fallback);
    public int I(string key, int fallback = 0) => (int)Math.Clamp(L(key, fallback), int.MinValue, int.MaxValue);
    public string S(string key, string fallback = "") => Value(key) is string text ? text : fallback;
    public bool B(string key, bool fallback = false) => Value(key) is bool flag ? flag : fallback;
    public DataMap Map(string key) => Value(key) as DataMap ?? new DataMap();
    public List<object?> List(string key) => Value(key) as List<object?> ?? (Value(key) is IEnumerable enumerable && Value(key) is not string ? enumerable.Cast<object?>().ToList() : new());
    public T? Get<T>(string key, T? fallback = default)
    {
        var value = Value(key);
        if (value is T typed)
            return typed;
        try
        {
            return value is null ? fallback : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException) { return fallback; }
    }
    public Godot.Vector3 Vector3(string key, Godot.Vector3 fallback = default)
    {
        if (Value(key) is Godot.Vector3 vector)
            return vector;
        var values = List(key);
        return values.Count == 3 ? new((float)Number(values[0]), (float)Number(values[1]), (float)Number(values[2])) : fallback;
    }
    public DataMap DeepClone() => (DataMap)Clone(this)!;
    public static object? Clone(object? value) => value switch
    {
        DataMap map => new DataMap(map.Select(pair => new KeyValuePair<string, object?>(pair.Key, Clone(pair.Value)))),
        IDictionary<string, object?> map => new DataMap(map.Select(pair => new KeyValuePair<string, object?>(pair.Key, Clone(pair.Value)))),
        IEnumerable sequence when value is not string => sequence.Cast<object?>().Select(Clone).ToList(),
        _ => value
    };
    public static double Number(object? value, double fallback = 0) => value switch
    {
        byte n => n,
        short n => n,
        int n => n,
        long n => n,
        float n => n,
        double n => n,
        decimal n => (double)n,
        _ => fallback
    };
    public static long Integer(object? value, long fallback = 0)
    {
        if (value is long integer)
            return integer;
        var number = Number(value, double.NaN);
        return double.IsFinite(number) && number >= long.MinValue && number < 9223372036854775808d ? (long)number : fallback;
    }
    public static bool ValidNumber(object? value, double minimum, double maximum, bool whole = false)
    {
        var number = Number(value, double.NaN);
        return double.IsFinite(number) && number >= minimum && number <= maximum && (!whole || Math.Floor(number) == number);
    }
    public static DataMap Parse(string json) => ParseValue(json) as DataMap ?? new();
    public static object? ParseValue(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
            return Read(document.RootElement);
        }
        catch (JsonException) { return null; }
    }
    private static object? Read(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => new DataMap(value.EnumerateObject().Select(p => new KeyValuePair<string, object?>(p.Name, Read(p.Value)))),
        JsonValueKind.Array => value.EnumerateArray().Select(Read).ToList(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out long n) ? n : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
    private static object? Ordered(object? value) => value switch
    {
        Godot.Vector2 v => new double[] { v.X, v.Y },
        Godot.Vector3 v => new double[] { v.X, v.Y, v.Z },
        Godot.Vector4 v => new double[] { v.X, v.Y, v.Z, v.W },
        Godot.Quaternion q => new double[] { q.X, q.Y, q.Z, q.W },
        Godot.Color c => new double[] { c.R, c.G, c.B, c.A },
        Godot.Basis b => new object?[] { Ordered(b.X), Ordered(b.Y), Ordered(b.Z) },
        Godot.Transform3D t => new object?[] { Ordered(t.Basis), Ordered(t.Origin) },
        DataMap map => new SortedDictionary<string, object?>(map.ToDictionary(p => p.Key, p => Ordered(p.Value)), StringComparer.Ordinal),
        IEnumerable list when value is not string => list.Cast<object?>().Select(Ordered).ToList(),
        _ => value
    };
    public string ToJson(bool indented = false) => JsonSerializer.Serialize(Ordered(this), new JsonSerializerOptions { WriteIndented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 128 });
    public static bool Equivalent(object? left, object? right)
    {
        if (left is DataMap a && right is DataMap b)
            return a.Count == b.Count && a.All(p => b.ContainsKey(p.Key) && Equivalent(p.Value, b[p.Key]));
        if (left is IEnumerable x && right is IEnumerable y && left is not string && right is not string)
            return x.Cast<object?>().SequenceEqual(y.Cast<object?>(), ValueComparer.Instance);
        if (ValidNumber(left, -double.MaxValue, double.MaxValue) && ValidNumber(right, -double.MaxValue, double.MaxValue))
            return Number(left) == Number(right);
        return Equals(left, right);
    }
    private sealed class ValueComparer : IEqualityComparer<object?>
    {
        internal static readonly ValueComparer Instance = new();
        public new bool Equals(object? x, object? y) => Equivalent(x, y);
        public int GetHashCode(object? value) => 0;
    }
}

public static class CatalogData
{
    // Benchmark infrastructure only: historical model formulas and JSON data remain unchanged.
    public static long Revision { get; private set; }
    public static CsvTable ReadCsv(string file) => CsvTable.Parse(_read(file), file);
    public static void ConfigureLegacyFixture(string rootDirectory) => Configure(rootDirectory);
    private static Func<string, string> _read = file => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "data", "domain", file));
    private static readonly Dictionary<string, DataMap> Cache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<DataMap>> RowCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, DataMap>> IndexCache = new(StringComparer.Ordinal);
    public static void Configure(string rootDirectory) => Configure(file => File.ReadAllText(Path.Combine(rootDirectory, file)));
    public static void Configure(Func<string, string> read)
    {
        _read = read;
        Revision++;
        Cache.Clear();
        RowCache.Clear();
        IndexCache.Clear();
    }
    public static DataMap Load(string file)
    {
        if (!Cache.TryGetValue(file, out var value))
        {
            value = DataMap.Parse(_read(file));
            if (value.Count == 0)
                throw new InvalidDataException("Missing or invalid domain catalog: " + file);
            Cache[file] = value;
        }
        return value;
    }
    public static IReadOnlyList<DataMap> Rows(string file, string key)
    {
        string cacheKey = file + ":" + key;
        if (!RowCache.TryGetValue(cacheKey, out var rows))
        {
            rows = Load(file).List(key).OfType<DataMap>().ToList().AsReadOnly();
            RowCache[cacheKey] = rows;
        }
        return rows;
    }
    public static DataMap Definition(string file, string key, string id)
    {
        string cacheKey = file + ":" + key;
        if (!IndexCache.TryGetValue(cacheKey, out var index))
        {
            index = Rows(file, key).ToDictionary(row => row.S("id"), StringComparer.Ordinal);
            IndexCache[cacheKey] = index;
        }
        return index.TryGetValue(id, out var row) ? row.DeepClone() : new();
    }
}
