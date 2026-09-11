using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
namespace Earthward.Domain;
/// <summary>Historical run identity uses Godot JSON.stringify ordering and number spelling.</summary>
public static class LegacyJson
{
    public static string Stringify(object? value)
    {
        if (value is DataMap map)
            return "{" + string.Join(",", map.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Quote(p.Key) + ":" + Stringify(p.Value))) + "}";
        if (value is List<object?> list)
            return "[" + string.Join(",", list.Select(Stringify)) + "]";
        if (value is null)
            return "null";
        if (value is bool flag)
            return flag ? "true" : "false";
        if (value is string text)
            return Quote(text);
        double number = DataMap.Number(value);
        if (!double.IsFinite(number))
            return "null";
        if (number == 0)
            return "0.0";
        // Godot4.6 JSON uses fixed-point, magnitude-adjusted decimal precision.
        // https://github.com/godotengine/godot/blob/4.6/core/io/json.cpp#L99
        int precision = Math.Clamp(14 - (int)Math.Floor(Math.Log10(Math.Abs(number))), 1, 32);
        string result;
        if (Math.Abs(number) < 1e17)
            result = number.ToString("F" + precision, CultureInfo.InvariantCulture);
        else
        {
            string sci = number.ToString("G17", CultureInfo.InvariantCulture).ToLowerInvariant();
            int e = sci.IndexOf('e');
            if (e < 0)
                result = sci + ".0";
            else
            {
                string mantissa = sci[..e];
                int exponent = int.Parse(sci[(e + 1)..], CultureInfo.InvariantCulture);
                bool negative = mantissa.StartsWith('-');
                if (negative)
                    mantissa = mantissa[1..];
                int dot = mantissa.IndexOf('.');
                if (dot < 0)
                    dot = mantissa.Length;
                string digits = mantissa.Replace(".", "");
                int decimalAt = dot + exponent;
                result = (negative ? "-" : "") + digits + new string('0', Math.Max(0, decimalAt - digits.Length)) + ".0";
            }
        }
        if (result.Contains('.'))
        {
            result = result.TrimEnd('0');
            if (result.EndsWith('.'))
                result += "0";
        }
        return result;
    }
    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\b", "\\b").Replace("\f", "\\f").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\v", "\\v").Replace("\"", "\\\"") + "\"";
}

