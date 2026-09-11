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
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 128 };
    private static readonly IComparer<KeyValuePair<string,object?>> JsonKeyOrder = Comparer<KeyValuePair<string,object?>>.Create((a,b)=>StringComparer.Ordinal.Compare(a.Key,b.Key));
    private static void WriteVector(Utf8JsonWriter writer,Vector3 vector)
    { writer.WriteStartArray();writer.WriteNumberValue((double)vector.X);writer.WriteNumberValue((double)vector.Y);writer.WriteNumberValue((double)vector.Z);writer.WriteEndArray(); }
    private static void WriteBasis(Utf8JsonWriter writer,Basis basis)
    { writer.WriteStartArray();WriteVector(writer,basis.X);WriteVector(writer,basis.Y);WriteVector(writer,basis.Z);writer.WriteEndArray(); }
    private static void WriteJsonValue(Utf8JsonWriter writer,object? value)
    {
        switch(value)
        {
            case null:writer.WriteNullValue();break;
            case string text:writer.WriteStringValue(text);break;
            case bool flag:writer.WriteBooleanValue(flag);break;
            case byte n:writer.WriteNumberValue(n);break;
            case sbyte n:writer.WriteNumberValue(n);break;
            case short n:writer.WriteNumberValue(n);break;
            case ushort n:writer.WriteNumberValue(n);break;
            case int n:writer.WriteNumberValue(n);break;
            case uint n:writer.WriteNumberValue(n);break;
            case long n:writer.WriteNumberValue(n);break;
            case ulong n:writer.WriteNumberValue(n);break;
            case float n:writer.WriteNumberValue(n);break;
            case double n:writer.WriteNumberValue(n);break;
            case decimal n:writer.WriteNumberValue(n);break;
            case Vector2 v:writer.WriteStartArray();writer.WriteNumberValue((double)v.X);writer.WriteNumberValue((double)v.Y);writer.WriteEndArray();break;
            case Vector3 v:WriteVector(writer,v);break;
            case Vector4 v:writer.WriteStartArray();writer.WriteNumberValue((double)v.X);writer.WriteNumberValue((double)v.Y);writer.WriteNumberValue((double)v.Z);writer.WriteNumberValue((double)v.W);writer.WriteEndArray();break;
            case Quaternion q:writer.WriteStartArray();writer.WriteNumberValue((double)q.X);writer.WriteNumberValue((double)q.Y);writer.WriteNumberValue((double)q.Z);writer.WriteNumberValue((double)q.W);writer.WriteEndArray();break;
            case Color c:writer.WriteStartArray();writer.WriteNumberValue((double)c.R);writer.WriteNumberValue((double)c.G);writer.WriteNumberValue((double)c.B);writer.WriteNumberValue((double)c.A);writer.WriteEndArray();break;
            case Basis basis:WriteBasis(writer,basis);break;
            case Transform3D transform:writer.WriteStartArray();WriteBasis(writer,transform.Basis);WriteVector(writer,transform.Origin);writer.WriteEndArray();break;
            case DataMap map:
                writer.WriteStartObject();
                // Sort only this map's key/value references; descendants are streamed without copying their graphs.
                if(map.Count<=1)
                {foreach(var(key,child)in map){writer.WritePropertyName(key);WriteJsonValue(writer,child);}}
                else if(map.Count==2)
                {
                    var iterator=map.GetEnumerator();iterator.MoveNext();var first=iterator.Current;iterator.MoveNext();var second=iterator.Current;
                    if(StringComparer.Ordinal.Compare(first.Key,second.Key)>0)(first,second)=(second,first);
                    writer.WritePropertyName(first.Key);WriteJsonValue(writer,first.Value);writer.WritePropertyName(second.Key);WriteJsonValue(writer,second.Value);
                }
                else
                {
                    var entries=map.ToArray();Array.Sort(entries,JsonKeyOrder);
                    foreach(var(key,child)in entries){writer.WritePropertyName(key);WriteJsonValue(writer,child);}
                }
                writer.WriteEndObject();break;
            case IEnumerable list:
                writer.WriteStartArray();foreach(var child in list)WriteJsonValue(writer,child);writer.WriteEndArray();break;
            default:JsonSerializer.Serialize(writer,value,value.GetType(),CanonicalJsonOptions);break;
        }
    }
    private System.Buffers.ArrayBufferWriter<byte> JsonBuffer(bool indented)
    {
        var buffer=new System.Buffers.ArrayBufferWriter<byte>(16384);
        using(var writer=new Utf8JsonWriter(buffer,new JsonWriterOptions{Indented=indented,Encoder=CanonicalJsonOptions.Encoder,MaxDepth=128}))
        {WriteJsonValue(writer,this);writer.Flush();}
        return buffer;
    }
    public byte[] ToJsonBytes(bool indented=false)=>JsonBuffer(indented).WrittenSpan.ToArray();
    public string ToJson(bool indented=false)=>System.Text.Encoding.UTF8.GetString(JsonBuffer(indented).WrittenSpan);
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
    private static Func<string, string> _read = file => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "data", "domain", file));
    private static readonly Dictionary<string, DataMap> Cache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<DataMap>> RowCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, DataMap>> IndexCache = new(StringComparer.Ordinal);
    public static long Revision { get; private set; }
    private static readonly Dictionary<string,CsvTable> CsvCache = new(StringComparer.Ordinal);
    public static CsvTable ReadCsv(string relativePath)
    {
        if(!relativePath.EndsWith(".csv",StringComparison.Ordinal)||relativePath.Contains("..")||Path.IsPathRooted(relativePath))throw new InvalidDataException("Invalid CSV catalog path: "+relativePath);
        if(!CsvCache.TryGetValue(relativePath,out var table)){table=CsvTable.Parse(_read(relativePath),relativePath);CsvCache[relativePath]=table;}return table;
    }
    private static bool _csvLoaded;
    public static void Configure(string rootDirectory) => Configure(file => File.ReadAllText(Path.Combine(rootDirectory, file)));
    public static void Configure(Func<string, string> read)
    {
        _read = read;
        Revision++;CsvCache.Clear();_csvLoaded=false;
        Cache.Clear();
        RowCache.Clear();
        IndexCache.Clear();
    }
    public static void ValidateRuntime() { Load("economy.json"); DomainBalance.ValidateReferences(); PerkEffectRules.Validate(); }
    public static DataMap Load(string file)
    {
        if (!_csvLoaded)
        {
            var loaded = CsvCatalogLoader.Load(ReadCsv);
            foreach(var (key,loadedValue) in loaded) Cache[key]=loadedValue;
            _csvLoaded=true;
        }
        return Cache.TryGetValue(file,out var catalog)?catalog:throw new InvalidDataException("Unknown runtime CSV catalog: "+file);
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

