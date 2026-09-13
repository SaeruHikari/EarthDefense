using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Numerics;

namespace SkrGui;

// Source: SkrGuiCore/batch/paint_context.hpp; src/batch/paint_context.cpp.
public sealed class PaintContext
{
    private BatchRoot? _batchRoot;
    private float _rootPixelRatio = 1, _aaRadius = 1;
    private bool _rootScaleOffsetOnly = true;
    private readonly List<PaintTransform> _transformStack = [];
    private readonly List<float> _opacityStack = [];
    private readonly List<Rectf> _clipBoundStack = [];
    private readonly List<bool> _clipRootPushedStack = [];
    private readonly List<BatchRoot> _rootStack = [];

    public PaintContext(float rootPixelRatio = 1, bool rootScaleOffsetOnly = true, float aaRadius = 1) => Reset(rootPixelRatio, rootScaleOffsetOnly, aaRadius);
    public void Reset(float rootPixelRatio = 1, bool rootScaleOffsetOnly = true, float aaRadius = 1)
    {
        _batchRoot = null; _rootStack.Clear(); _transformStack.Clear(); _clipBoundStack.Clear(); _clipRootPushedStack.Clear(); _opacityStack.Clear();
        Debug.Assert(float.IsFinite(rootPixelRatio) && rootPixelRatio > 0);
        Debug.Assert(float.IsFinite(aaRadius) && aaRadius >= 0);
        _rootPixelRatio = float.IsFinite(rootPixelRatio) && rootPixelRatio > 0 ? rootPixelRatio : 1;
        _rootScaleOffsetOnly = rootScaleOffsetOnly;
        _aaRadius = float.IsFinite(aaRadius) && aaRadius >= 0 ? aaRadius : 1;
    }
    public BatchRoot? BatchRoot() => _batchRoot;
    public float RootPixelRatio() => _rootPixelRatio;
    public bool RootScaleOffsetOnly() => _rootScaleOffsetOnly;
    public float AaRadius() => _aaRadius;
    private static readonly PaintTransform IdentityTransform = new();
    public ref readonly PaintTransform Transform() { if(_transformStack.Count==0)return ref IdentityTransform;return ref CollectionsMarshal.AsSpan(_transformStack)[^1]; }
    public float PixelRatio() => _rootPixelRatio * Transform().PixelRatioScale();
    public bool ScaleOffsetOnly() => _rootScaleOffsetOnly && Transform().ScaleOffsetOnly();
    public float Opacity() => _opacityStack.Count == 0 ? 1 : _opacityStack[^1];
    public bool HasClipBound() => _clipBoundStack.Count != 0;
    public Rectf ClipBound() => HasClipBound() ? _clipBoundStack[^1] : Rectf.Largest();
    public Rectf TransformBound(Rectf localBound) => Transform().TransformRect(localBound);
    public Rectf ClippedBound(Rectf paintBound) => HasClipBound() ? paintBound.Intersect(_clipBoundStack[^1]) : paintBound;
    public Rectf ClippedLocalBound(Rectf localBound) => ClippedBound(TransformBound(localBound));
    public bool IsBoundVisible(Rectf paintBound) => !ClippedBound(paintBound).IsEmpty();
    public bool IsLocalBoundVisible(Rectf localBound) => !ClippedLocalBound(localBound).IsEmpty();

    public bool ResolvePixelMapping(out float pixelRatio, out Offsetf deviceOffset)
    {
        static bool NearlyZero(float value) => MathF.Abs(value) <= 1e-5f;
        static bool NearlyEqual(float lhs, float rhs) => MathF.Abs(lhs - rhs) <= 1e-5f * CppMath.Max(1, CppMath.Max(MathF.Abs(lhs), MathF.Abs(rhs)));
        pixelRatio = 0; deviceOffset = Offsetf.Zero();
        if (!ScaleOffsetOnly()) return false;
        var current = Transform(); var origin = current.TransformPoint(Offsetf.Zero());
        var axisX = current.TransformPoint(new Offsetf(1, 0)) - origin;
        var axisY = current.TransformPoint(new Offsetf(0, 1)) - origin;
        if (!origin.IsFinite() || !axisX.IsFinite() || !axisY.IsFinite() || !NearlyZero(axisX.Y) || !NearlyZero(axisY.X) || !NearlyEqual(axisX.X, axisY.Y) || axisX.X <= 0 || axisY.Y <= 0) return false;
        pixelRatio = _rootPixelRatio * (axisX.X + axisY.Y) * .5f;
        deviceOffset = origin * _rootPixelRatio;
        return float.IsFinite(pixelRatio) && pixelRatio > 0 && deviceOffset.IsFinite();
    }
    public void PushTransform(PaintTransform transform) { var next = Transform(); next.Apply(transform); _transformStack.Add(next); }
    public void PushTransform(Float3x3 transform) { var next = Transform(); next.Apply(transform); _transformStack.Add(next); }
    public void PushTransform(Matrix4x4 transform) { var next = Transform(); next.Apply(transform); _transformStack.Add(next); }
    public void PopTransform() { Debug.Assert(_transformStack.Count != 0); if (_transformStack.Count != 0) _transformStack.RemoveAt(_transformStack.Count - 1); }
    public void PushOpacity(float opacity) { Debug.Assert(float.IsFinite(opacity)); _opacityStack.Add(Opacity() * opacity); }
    public void PushOpacityOverride(float opacity) { Debug.Assert(float.IsFinite(opacity)); _opacityStack.Add(opacity); }
    public void PopOpacity() { Debug.Assert(_opacityStack.Count != 0); if (_opacityStack.Count != 0) _opacityStack.RemoveAt(_opacityStack.Count - 1); }

    public BatchCmdClip? PushClipRect(Rectf localRect)
    {
        var bound = ClippedLocalBound(localRect);
        if (bound.IsEmpty()) { PushClipBoundOnly(bound); return null; }
        BatchCmdClip cmd = ScaleOffsetOnly() ? new BatchCmdClipRect { Bound = bound } : new BatchCmdClipMesh { Mesh = MakeRectMesh(localRect), Bound = bound, Transform = Transform().ResolvedTransform() };
        PushClipRoot(cmd, bound); return cmd;
    }
    public BatchCmdClipMesh? PushClipMesh(Mesh? mesh, Rectf localBound)
    {
        if (mesh is null) return null;
        var bound = ClippedLocalBound(localBound);
        if (bound.IsEmpty()) { PushClipBoundOnly(bound); return null; }
        var cmd = new BatchCmdClipMesh { Mesh = mesh, Bound = bound, Transform = Transform().ResolvedTransform() };
        PushClipRoot(cmd, bound); return cmd;
    }
    public void PopClip()
    {
        Debug.Assert(_clipBoundStack.Count != 0 && _clipRootPushedStack.Count == _clipBoundStack.Count);
        if (_clipBoundStack.Count == 0) return;
        if (_clipRootPushedStack[^1])
        {
            Debug.Assert(_rootStack.Count > 1); if (_rootStack.Count <= 1) return;
            _rootStack.RemoveAt(_rootStack.Count - 1);
        }
        _clipBoundStack.RemoveAt(_clipBoundStack.Count - 1); _clipRootPushedStack.RemoveAt(_clipRootPushedStack.Count - 1);
    }
    public BatchCmdDrawMesh? DrawMesh(Mesh? mesh, Texture? texture, Shader? shader, Rectf localBound)
    {
        if (mesh is null) return null; var bound = ClippedLocalBound(localBound); if (bound.IsEmpty()) return null;
        var cmd = new BatchCmdDrawMesh { Mesh = mesh, Texture = texture, Shader = shader, Bound = bound, Transform = Transform().ResolvedTransform() };
        EnsureCurrentRoot().Cmds.Add(cmd); return cmd;
    }
    public BatchCmdText? DrawText(Mesh? mesh, uint atlasIndex, ETextPixelMode pixelMode, Rectf localBound, object? source)
    {
        if (mesh is null || atlasIndex == uint.MaxValue || source is null) return null;
        var bound = ClippedLocalBound(localBound); if (bound.IsEmpty()) return null;
        var cmd = new BatchCmdText { Mesh = mesh, Bound = bound, Transform = Transform().ResolvedTransform(), AtlasIndex = atlasIndex, PixelMode = pixelMode, Source = source };
        EnsureCurrentRoot().Cmds.Add(cmd); return cmd;
    }
    public BatchCmdAtlas? DrawAtlas(Mesh? mesh, uint atlasIndex, Rectf localBound)
    {
        if (mesh is null || atlasIndex == uint.MaxValue) return null;
        var bound = ClippedLocalBound(localBound); if (bound.IsEmpty()) return null;
        var cmd = new BatchCmdAtlas { Mesh = mesh, Bound = bound, Transform = Transform().ResolvedTransform(), AtlasIndex = atlasIndex };
        EnsureCurrentRoot().Cmds.Add(cmd); return cmd;
    }
    public BatchCmdBackdrop? Backdrop(BatchCmdBackdrop? command, Mesh? mesh, Rectf localBound)
    {
        if (command is null || mesh is null) return null;
        var bound = ClippedLocalBound(localBound); if (bound.IsEmpty()) return null;
        command.Mesh = mesh; command.Bound = bound; command.Transform = Transform().ResolvedTransform();
        EnsureCurrentRoot().Cmds.Add(command); return command;
    }
    private void PushClipBoundOnly(Rectf clipBound) { _clipBoundStack.Add(clipBound); _clipRootPushedStack.Add(false); }
    private BatchRoot EnsureCurrentRoot()
    {
        _batchRoot ??= new BatchRoot(); if (_rootStack.Count == 0) _rootStack.Add(_batchRoot); return _rootStack[^1];
    }
    private void PushClipRoot(BatchCmdClip cmd, Rectf clipBound)
    {
        var subRoot = new BatchRoot(); cmd.SubRoot = subRoot; EnsureCurrentRoot().Cmds.Add(cmd);
        _rootStack.Add(subRoot); _clipBoundStack.Add(clipBound); _clipRootPushedStack.Add(true);
    }
    private static Mesh MakeRectMesh(Rectf rect)
    {
        var mesh = new Mesh();
        mesh.Vertices.Add(new MeshVertex(rect.TopLeft(), Offsetf.Zero(), 0xffffffff));
        mesh.Vertices.Add(new MeshVertex(rect.TopRight(), Offsetf.Zero(), 0xffffffff));
        mesh.Vertices.Add(new MeshVertex(rect.BottomRight(), Offsetf.Zero(), 0xffffffff));
        mesh.Vertices.Add(new MeshVertex(rect.BottomLeft(), Offsetf.Zero(), 0xffffffff));
        mesh.PushQuad(0, 1, 2, 3); return mesh;
    }
}
