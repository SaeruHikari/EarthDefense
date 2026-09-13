using System.Diagnostics;

namespace SkrGui;

public enum EVGPathCommandType : byte { MoveTo, LineTo, QuadTo, CubicTo, Close }
public enum EVGPathCommandState : byte { Rest, PrepareDraw, Drawing }
public struct VGPathCommand
{
    public struct MoveToData { public Offsetf Target; }
    public struct LineToData { public Offsetf Target; }
    public struct QuadToData { public Offsetf Control, Target; }
    public struct CubicToData { public Offsetf Control0, Control1, Target; }
    public struct CloseData { public EVGPathWinding? WindingHint; }
    public EVGPathCommandType Type;
    public MoveToData Move;
    public LineToData Line;
    public QuadToData Quad;
    public CubicToData Cubic;
    public CloseData Closing;
    public static VGPathCommand MoveTo(Offsetf target) => new() { Type = EVGPathCommandType.MoveTo, Move = new() { Target = target } };
    public static VGPathCommand LineTo(Offsetf target) => new() { Type = EVGPathCommandType.LineTo, Line = new() { Target = target } };
    public static VGPathCommand QuadTo(Offsetf control, Offsetf target) => new() { Type = EVGPathCommandType.QuadTo, Quad = new() { Control = control, Target = target } };
    public static VGPathCommand CubicTo(Offsetf control0, Offsetf control1, Offsetf target) => new() { Type = EVGPathCommandType.CubicTo, Cubic = new() { Control0 = control0, Control1 = control1, Target = target } };
    public static VGPathCommand Close(EVGPathWinding? windingHint = null) => new() { Type = EVGPathCommandType.Close, Closing = new() { WindingHint = windingHint } };
}
public struct VGPathFlattenOptions
{
    public float PixelRatio = 1, TessellationFactor = 1;
    public uint MaxDepth = 10;
    public VGPathFlattenOptions() { }
}

// Source: src/vg/vg_path.cpp. Command writes preserve the original validation/no-op behavior.
public sealed partial class VGPath
{
    public VGPath() {}
    public VGPath(VGPath source) { PointEqualsTolerance=source.PointEqualsTolerance; foreach(var command in source._commands.AsSpan())_commands.PushBack(command); }
    public float PointEqualsTolerance = .01f;
    private readonly VgBuffer<VGPathCommand> _commands = new();
    public bool IsEmpty() => _commands.IsEmpty();
    public EVGPathCommandState State() => _commands.IsEmpty() ? EVGPathCommandState.Rest : _commands.AtLast().Type switch
    {
        EVGPathCommandType.MoveTo => EVGPathCommandState.PrepareDraw,
        EVGPathCommandType.LineTo or EVGPathCommandType.QuadTo or EVGPathCommandType.CubicTo => EVGPathCommandState.Drawing,
        _ => EVGPathCommandState.Rest
    };
    public Offsetf? CursorPos()
    {
        if (_commands.IsEmpty()) return null; var tail = _commands.AtLast();
        return tail.Type switch { EVGPathCommandType.MoveTo => tail.Move.Target, EVGPathCommandType.LineTo => tail.Line.Target, EVGPathCommandType.QuadTo => tail.Quad.Target, EVGPathCommandType.CubicTo => tail.Cubic.Target, _ => null };
    }
    public IReadOnlyList<VGPathCommand> Commands() => _commands;
    public void Reserve(ulong commandCapacity) => _commands.Reserve(commandCapacity);
    public void Clear() => _commands.Clear();
    public void Release(ulong commandCapacity = 0) => _commands.Release(commandCapacity);
    public void MoveTo(Offsetf to)
    {
        if (!to.IsFinite()) { Debug.Assert(false, "MoveTo requires a finite target"); return; }
        if (State() == EVGPathCommandState.PrepareDraw && !_commands.IsEmpty())
        {
            ref var tail = ref _commands.AtLast();
            if (tail.Type == EVGPathCommandType.MoveTo) { if (!tail.Move.Target.NearlyEqual(to, PointEqualsTolerance)) tail.Move.Target = to; return; }
        }
        _commands.PushBack(VGPathCommand.MoveTo(to));
    }
    public void LineTo(Offsetf to)
    {
        var cursor = CursorPos();
        if (!cursor.HasValue || !to.IsFinite()) { Debug.Assert(false, "LineTo requires an active cursor and finite target"); return; }
        if (cursor.Value.NearlyEqual(to, PointEqualsTolerance)) { Debug.Assert(false, "LineTo requires a nonzero segment"); return; }
        _commands.PushBack(VGPathCommand.LineTo(to));
    }
    public void QuadTo(Offsetf control, Offsetf to)
    {
        var cursor = CursorPos();
        if (!cursor.HasValue || !control.IsFinite() || !to.IsFinite()) { Debug.Assert(false, "QuadTo requires an active cursor and finite points"); return; }
        if (cursor.Value.NearlyEqual(control, PointEqualsTolerance) && cursor.Value.NearlyEqual(to, PointEqualsTolerance)) { Debug.Assert(false, "QuadTo requires a nonzero curve"); return; }
        _commands.PushBack(VGPathCommand.QuadTo(control, to));
    }
    public void CubicTo(Offsetf control0, Offsetf control1, Offsetf to)
    {
        var cursor = CursorPos();
        if (!cursor.HasValue || !control0.IsFinite() || !control1.IsFinite() || !to.IsFinite()) { Debug.Assert(false, "CubicTo requires an active cursor and finite points"); return; }
        if (cursor.Value.NearlyEqual(control0, PointEqualsTolerance) && cursor.Value.NearlyEqual(control1, PointEqualsTolerance) && cursor.Value.NearlyEqual(to, PointEqualsTolerance)) { Debug.Assert(false, "CubicTo requires a nonzero curve"); return; }
        _commands.PushBack(VGPathCommand.CubicTo(control0, control1, to));
    }
    public void Close(EVGPathWinding? windingHint = null)
    {
        var state = State();
        if (state == EVGPathCommandState.PrepareDraw && !_commands.IsEmpty()) { _commands.PushBack(VGPathCommand.Close(windingHint)); return; }
        if (state != EVGPathCommandState.Drawing) { Debug.Assert(false, "Close requires an active subpath"); return; }
        _commands.PushBack(VGPathCommand.Close(windingHint));
    }
    public void AddRect(Rectf rect, EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!rect.IsFinite()) { Debug.Assert(false, "AddRect requires a finite rectangle"); return; }
        if (rect.IsEmpty()) return; MoveTo(rect.TopLeft());
        if (winding == EVGPathWinding.CW) { LineTo(rect.TopRight()); LineTo(rect.BottomRight()); LineTo(rect.BottomLeft()); }
        else { LineTo(rect.BottomLeft()); LineTo(rect.BottomRight()); LineTo(rect.TopRight()); }
        Close(winding);
    }
}
