using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;
/// <summary>Safe legacy-compatible codec. No engine objects, scripts or native Variant dictionaries.</summary>
public static class CombatSnapshotCodec
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataMap, HashSet<string>> NumericKeys = new();
    public static bool IsNumericKey(DataMap map, string key) => NumericKeys.TryGetValue(map, out var keys) && keys.Contains(key);
    public static object? Encode(object? value, string context = "")
    {
        if (value is null or bool or string)
            return value;
        if (value is byte or short or int or long or sbyte or ushort or uint)
            return new DataMap { ["@"] = "int", ["v"] = new List<object?> { Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) } };
        if (value is float or double or decimal)
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (value is Vector3 p)
            return new DataMap { ["@"] = "v3", ["v"] = new List<object?> { (double)p.X, (double)p.Y, (double)p.Z } };
        if (value is Color c)
            return new DataMap { ["@"] = "color", ["v"] = new List<object?> { (double)c.R, (double)c.G, (double)c.B, (double)c.A } };
        if (value is Basis b)
            return new DataMap { ["@"] = "basis", ["v"] = new List<object?> { Encode(b.Column0), Encode(b.Column1), Encode(b.Column2) } };
        if (value is IDictionary map)
        {
            var list = new List<object?>();
            foreach (DictionaryEntry pair in map)
            {
                object key = pair.Key;
                if (context is "_factories" or "missile_chain" or "occupied" && key is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    key = n;
                list.Add(new List<object?> { Encode(key), Encode(pair.Value, Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? "") });
            }
            return new DataMap { ["@"] = "map", ["v"] = list };
        }
        if (value is IEnumerable seq)
        {
            var result = new List<object?>();
            foreach (var item in seq)
                result.Add(Encode(item));
            return result;
        }
        throw new ArgumentException("Unsupported snapshot value " + value.GetType());
    }
    public static bool TryDecode(object? encoded, out object? decoded)
    {
        int budget = 600000;
        try
        {
            decoded = Decode(encoded, 0, ref budget);
            return true;
        }
        catch (FormatException) { decoded = null; return false; }
        catch (OverflowException) { decoded = null; return false; }
    }
    private static object? Decode(object? value, int depth, ref int budget)
    {
        if (depth > 32 || --budget < 0)
            throw new FormatException();
        if (value is double d && !double.IsFinite(d) || value is float f && !float.IsFinite(f))
            throw new FormatException();
        if (value is List<object?> list)
        {
            var result = new List<object?>();
            foreach (var item in list)
                result.Add(Decode(item, depth + 1, ref budget));
            return result;
        }
        if (value is DataMap map)
        {
            if (map.Count != 2 || map.Value("@") is not string tag || map.Value("v") is not List<object?> raw)
                throw new FormatException();
            if (tag == "int")
            {
                if (raw.Count != 1 || raw[0] is not string text || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n) || n.ToString(CultureInfo.InvariantCulture) != text)
                    throw new FormatException();
                return n;
            }
            if (tag == "map")
            {
                var result = new DataMap();
                var numeric = new HashSet<string>();
                NumericKeys.Add(result, numeric);
                foreach (var item in raw)
                {
                    if (item is not List<object?> pair || pair.Count != 2)
                        throw new FormatException();
                    var key = Decode(pair[0], depth + 1, ref budget);
                    var child = Decode(pair[1], depth + 1, ref budget);
                    if (key is not (string or long or int or double))
                        throw new FormatException();
                    string name = Convert.ToString(key, CultureInfo.InvariantCulture)!;
                    if (result.ContainsKey(name))
                        throw new FormatException();
                    result[name] = child;
                    if (key is not string)
                        numeric.Add(name);
                }
                return result;
            }
            if (tag is "v3" or "color")
            {
                if (raw.Count != (tag == "v3" ? 3 : 4) || raw.Any(v => !IsNumber(v)))
                    throw new FormatException();
                return tag == "v3" ? C.Vec(DataMap.Number(raw[0]), DataMap.Number(raw[1]), DataMap.Number(raw[2])) : new Color((float)DataMap.Number(raw[0]), (float)DataMap.Number(raw[1]), (float)DataMap.Number(raw[2]), (float)DataMap.Number(raw[3]));
            }
            if (tag == "basis" && raw.Count == 3)
            {
                var axes = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    if (Decode(raw[i], depth + 1, ref budget) is not Vector3 axis)
                        throw new FormatException();
                    axes[i] = axis;
                }
                return new Basis(axes[0], axes[1], axes[2]);
            }
            throw new FormatException();
        }
        if (value is null or bool or string or long or int or double or float)
            return value;
        throw new FormatException();
    }
    public static bool IsNumber(object? v) => (v is byte or short or int or long or float or double or decimal) && double.IsFinite(DataMap.Number(v, double.NaN));
    public static bool IsInteger(object? v) => IsNumber(v) && DataMap.Number(v) == Math.Floor(DataMap.Number(v)) && Math.Abs(DataMap.Number(v)) <= 9007199254740991d;
    public static bool HasFields(DataMap? map, string fields)
    {
        if (map == null)
            return false;
        foreach (var spec in fields.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = spec.Split(':');
            if (!map.TryGetValue(parts[0], out var v))
                return false;
            bool valid = parts[1] switch
            {
                "number" => IsNumber(v),
                "int" => IsInteger(v),
                "text" => v is string,
                "bool" => v is bool,
                "v3" => v is Vector3 p && p.IsFinite(),
                "basis" => v is Basis b && b.IsFinite() && Math.Abs(b.Determinant()) >= .00001,
                "color" => v is Color,
                "array" => v is List<object?>,
                "map" => v is DataMap,
                _ => false
            };
            if (!valid)
                return false;
        }
        return true;
    }
}


