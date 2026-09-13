using System.Runtime.InteropServices;

namespace SkrGui;

// Source: SkrGuiCore/batch/color_gradient.hpp.
public enum EColorGradientAxis : byte { LeftToRight, RightToLeft, TopToBottom, BottomToTop }
public struct ColorGradientStop(float position, SRGBColor color)
{
    public ColorGradientStop():this(0,new SRGBColor()) {}
    public float Position = position;
    public SRGBColor Color = color;
    public readonly bool IsValid() => float.IsFinite(Position) && Color.IsFinite();
}
public sealed class ColorGradientSolid
{
    private SRGBColor _color = new(1, 1, 1, 1);
    public ColorGradientSolid() { }
    public ColorGradientSolid(ColorGradientSolid source) => _color = source._color;
    public ColorGradientSolid(SRGBColor color) => _color = color;
    public SRGBColor Color() => _color;
    public bool IsValid() => _color.IsFinite();
    public void SetColor(SRGBColor color) => _color = color;
    public SRGBColor Sample(float t) => _color;
    public SRGBColor Sample(Offsetf uv) => _color;
}
public sealed class ColorGradientAxis
{
    private SRGBColor _startColor = new(), _endColor = new();
    private EColorGradientAxis _axis;
    public ColorGradientAxis() { }
    public ColorGradientAxis(ColorGradientAxis source) { _startColor=source._startColor;_endColor=source._endColor;_axis=source._axis; }
    public ColorGradientAxis(SRGBColor startColor, SRGBColor endColor, EColorGradientAxis axis = EColorGradientAxis.LeftToRight) { _startColor = startColor; _endColor = endColor; _axis = axis; }
    public SRGBColor StartColor() => _startColor;
    public SRGBColor EndColor() => _endColor;
    public EColorGradientAxis Axis() => _axis;
    public bool IsValid() => _axis is >= EColorGradientAxis.LeftToRight and <= EColorGradientAxis.BottomToTop && _startColor.IsFinite() && _endColor.IsFinite();
    public void SetStartColor(SRGBColor color) => _startColor = color;
    public void SetEndColor(SRGBColor color) => _endColor = color;
    public void SetAxis(EColorGradientAxis axis) => _axis = axis;
    public SRGBColor Sample(float t) => SRGBColor.Lerp(_startColor, _endColor, SanitizeT(t));
    public SRGBColor Sample(Offsetf uv) => Sample(_axis switch { EColorGradientAxis.LeftToRight => uv.X, EColorGradientAxis.RightToLeft => 1 - uv.X, EColorGradientAxis.TopToBottom => uv.Y, EColorGradientAxis.BottomToTop => 1 - uv.Y, _ => 0 });
    private static float SanitizeT(float t) => float.IsFinite(t) ? CppMath.Clamp(t, 0, 1) : 0;
}
public sealed class ColorGradientCurve
{
    private readonly List<ColorGradientStop> _stops = [];
    private Offsetf _start = new(0, 0), _end = new(1, 0);
    public ColorGradientCurve() { }
    public ColorGradientCurve(ReadOnlySpan<ColorGradientStop> stops) => Reset(stops, new Offsetf(0, 0), new Offsetf(1, 0));
    public ColorGradientCurve(ReadOnlySpan<ColorGradientStop> stops, Offsetf start) => Reset(stops,start,new Offsetf(1,0));
    public ColorGradientCurve(ReadOnlySpan<ColorGradientStop> stops, Offsetf start, Offsetf end) => Reset(stops, start, end);
    public ColorGradientCurve(ColorGradientCurve source) => Reset(source.Stops(), source.Start(), source.End());
    public ReadOnlySpan<ColorGradientStop> Stops() => CollectionsMarshal.AsSpan(_stops);
    public Offsetf Start() => _start;
    public Offsetf End() => _end;
    public bool IsValid()
    {
        if (!_start.IsFinite() || !_end.IsFinite() || _stops.Count == 0) return false;
        float length = (_end - _start).LengthSquared(); if (!float.IsFinite(length) || length <= 0) return false;
        foreach (var stop in _stops) if (!stop.IsValid()) return false;
        return true;
    }
    public void SetStops(ReadOnlySpan<ColorGradientStop> stops)
    {
        // Snapshot also preserves the original vector-copy overload when the caller passes our own storage.
        var copy = stops.ToArray(); _stops.Clear(); _stops.AddRange(copy); NormalizeStops();
    }
    public void AddStop(float position, SRGBColor color) => AddStop(new ColorGradientStop(position, color));
    public void AddStop(ColorGradientStop stop)
    {
        stop.Position = SanitizeT(stop.Position); int left = 0, right = _stops.Count;
        while (left < right) { int mid = left + (right - left) / 2; if (_stops[mid].Position < stop.Position) left = mid + 1; else right = mid; }
        if (left < _stops.Count && _stops[left].Position == stop.Position) { _stops[left] = stop; return; }
        _stops.Insert(left, stop);
    }
    public void SetStart(Offsetf start) => _start = start;
    public void SetEnd(Offsetf end) => _end = end;
    public void Reset(ReadOnlySpan<ColorGradientStop> stops) => Reset(stops,new Offsetf(0,0),new Offsetf(1,0));
    public void Reset(ReadOnlySpan<ColorGradientStop> stops,Offsetf start) => Reset(stops,start,new Offsetf(1,0));
    public void Reset(ReadOnlySpan<ColorGradientStop> stops, Offsetf start, Offsetf end) { _start = start; _end = end; SetStops(stops); }
    public SRGBColor Sample(float t)
    {
        if (_stops.Count == 0) return new SRGBColor(); if (_stops.Count == 1) return _stops[0].Color;
        t = SanitizeT(t); if (t <= _stops[0].Position) return _stops[0].Color; if (t >= _stops[^1].Position) return _stops[^1].Color;
        for (int i = 1; i < _stops.Count; ++i)
        {
            var next = _stops[i]; if (t > next.Position) continue;
            var previous = _stops[i - 1]; float span = next.Position - previous.Position;
            return SRGBColor.Lerp(previous.Color, next.Color, span > 0 ? (t - previous.Position) / span : 1);
        }
        return _stops[^1].Color;
    }
    public SRGBColor Sample(Offsetf uv) => Sample(ProjectT(_start, _end, uv));
    private static float SanitizeT(float t) => float.IsFinite(t) ? CppMath.Clamp(t, 0, 1) : 0;
    private static float ProjectT(Offsetf start, Offsetf end, Offsetf uv)
    {
        if (!start.IsFinite() || !end.IsFinite() || !uv.IsFinite()) return 0;
        var axis = end - start; float length = axis.LengthSquared();
        return !float.IsFinite(length) || length <= 0 ? 0 : SanitizeT((uv - start).Dot(axis) / length);
    }
    private void NormalizeStops()
    {
        if (_stops.Count == 0) return;
        var ordered = _stops.Select((stop, index) => { stop.Position = SanitizeT(stop.Position); return (stop, index); }).ToArray();
        Array.Sort(ordered, (a, b) => { int c = a.stop.Position.CompareTo(b.stop.Position); return c != 0 ? c : a.index.CompareTo(b.index); });
        for (int i = 0; i < ordered.Length; ++i) _stops[i] = ordered[i].stop;
        int writeIndex = 0;
        for (int readIndex = 0; readIndex < _stops.Count; ++readIndex)
        {
            if (writeIndex > 0 && _stops[writeIndex - 1].Position == _stops[readIndex].Position) { _stops[writeIndex - 1] = _stops[readIndex]; continue; }
            if (writeIndex != readIndex) _stops[writeIndex] = _stops[readIndex]; ++writeIndex;
        }
        if (writeIndex < _stops.Count) _stops.RemoveRange(writeIndex, _stops.Count - writeIndex);
    }
}
public sealed class ColorGradient
{
    private enum Kind : byte { Solid, Axis, Curve }
    private Kind _kind;
    private ColorGradientSolid _solid = new();
    private ColorGradientAxis _axis = new();
    private ColorGradientCurve? _curve;
    public ColorGradient() { }
    public ColorGradient(ColorGradientSolid solid) { _kind = Kind.Solid; _solid = new(solid); }
    public ColorGradient(ColorGradientAxis axis) { _kind = Kind.Axis; _axis = new(axis); }
    public ColorGradient(ColorGradientCurve curve) { _kind = Kind.Curve; _curve = new ColorGradientCurve(curve); }
    public ColorGradient(ColorGradient other) { _kind = other._kind; _solid = new(other._solid); _axis = new(other._axis); _curve = other._curve is null ? null : new ColorGradientCurve(other._curve); }
    public static implicit operator ColorGradient(ColorGradientSolid value) => new(value);
    public static implicit operator ColorGradient(ColorGradientAxis value) => new(value);
    public static implicit operator ColorGradient(ColorGradientCurve value) => new(value);
    public bool IsSolid() => _kind == Kind.Solid;
    public bool IsAxis() => _kind == Kind.Axis;
    public bool IsCurve() => _kind == Kind.Curve;
    public bool IsValid() => _kind switch { Kind.Solid => _solid.IsValid(), Kind.Axis => _axis.IsValid(), Kind.Curve => _curve!.IsValid(), _ => false };
    public ColorGradientSolid? AsSolid() => IsSolid() ? _solid : null;
    public ColorGradientAxis? AsAxis() => IsAxis() ? _axis : null;
    public ColorGradientCurve? AsCurve() => IsCurve() ? _curve : null;
    public SRGBColor Sample(float t) => _kind switch { Kind.Solid => _solid.Sample(t), Kind.Axis => _axis.Sample(t), Kind.Curve => _curve!.Sample(t), _ => new SRGBColor() };
    public SRGBColor Sample(Offsetf uv) => _kind switch { Kind.Solid => _solid.Sample(uv), Kind.Axis => _axis.Sample(uv), Kind.Curve => _curve!.Sample(uv), _ => new SRGBColor() };
    public SRGBColor Sample(Rectf bounds, Offsetf point)
    {
        float width = bounds.Width(), height = bounds.Height();
        float x = width > 0 ? (point.X - bounds.Left) / width : 0, y = height > 0 ? (point.Y - bounds.Top) / height : 0;
        return Sample(new Offsetf(x, y));
    }
}
