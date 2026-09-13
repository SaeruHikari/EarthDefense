using System.Numerics;
using System.Runtime.InteropServices;

namespace SkrGui;

// Source: visual/backend/visual_owner.hpp and src/visual/backend/visual_owner.cpp, 611561f8.
public sealed class VisualOwner : IDisposable
{
    private readonly TextServices _textServices;
    private readonly VisualBackend _visualBackend;
    private readonly List<BatchRule> _batchRules = [];
    private VisualNode? _root;
    private List<VisualNode> _nodesNeedingLayout = [];
    private BoxConstraints _lastLayoutConstraints = new();
    private bool _hasLaidOut;
    private BatchRoot? _paintedRoot;
    private Sizef _paintedLogicSize;
    private readonly List<MeshVertex> _vertices = [];
    private readonly List<uint> _indices = [];
    private bool _hasPainted;

    public VisualOwner(TextServices textServices, VisualBackend visualBackend)
    {
        _textServices = textServices ?? throw new ArgumentNullException(nameof(textServices));
        _visualBackend = visualBackend ?? throw new ArgumentNullException(nameof(visualBackend));
    }
    public void Dispose() => ClearRoot();
    public TextServices TextServices() => _textServices;
    public VisualBackend VisualBackend() => _visualBackend;
    public VisualNode? Root() => _root;
    public void AddBatchRule(BatchRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        foreach (var current in _batchRules) if (ReferenceEquals(current, rule)) return;
        _batchRules.Add(rule);
    }
    public void RemoveBatchRule(BatchRule? rule)
    {
        for (int i = 0; i < _batchRules.Count; i++)
            if (ReferenceEquals(_batchRules[i], rule)) { _batchRules.RemoveAt(i); return; }
    }
    public void ClearBatchRules() => _batchRules.Clear();
    public void SetRoot(VisualNode? root)
    {
        if (ReferenceEquals(_root, root)) return;
        if (root != null && (root.Parent() != null || root.Owner() != null))
            throw new InvalidOperationException("VisualOwner root must have no parent or existing owner");
        ClearRoot(); _root = root;
        _root?.AttachOwnerSubtree(this, 0);
        _nodesNeedingLayout.Clear(); _hasLaidOut = false;
    }
    public VisualNode? RemoveRoot()
    {
        ClearPaint(); _nodesNeedingLayout.Clear(); _hasLaidOut = false;
        var root = _root; _root = null;
        if (root != null)
        {
            if (root.Parent() != null || !ReferenceEquals(root.Owner(), this)) throw new InvalidOperationException("VisualOwner root ownership is inconsistent");
            root.DetachOwnerSubtree();
        }
        return root;
    }
    public void ClearRoot() => RemoveRoot();
    internal void ScheduleLayout(VisualNode? node)
    {
        if (node == null) return;
        foreach (var scheduled in _nodesNeedingLayout) if (ReferenceEquals(scheduled, node)) return;
        _nodesNeedingLayout.Add(node);
    }
    private bool FlushLayout(BoxConstraints rootConstraints)
    {
        if (_nodesNeedingLayout.Count == 0) return false;
        var pending = _nodesNeedingLayout.OrderBy(node => node.VisualDepth()).ToList();
        _nodesNeedingLayout = [];
        bool relaidOut = false;
        foreach (var node in pending)
        {
            if (node == null) continue;
            if (ReferenceEquals(node, _root)) { relaidOut = node.Layout(rootConstraints) || relaidOut; continue; }
            if (!node.NeedsLayout()) continue;
            node.Layout(node.Constraints()); relaidOut = true;
        }
        return relaidOut;
    }
    public bool Layout(BoxConstraints constraints)
    {
        ClearPaint(); _lastLayoutConstraints = constraints; _hasLaidOut = true;
        if (_root == null) return false;
        ScheduleLayout(_root); return FlushLayout(constraints);
    }
    public bool HitTest(VisualHitTestResult result, Offsetf position) => _root?.HitTest(result, position) ?? false;
    public bool Paint() => Paint(new VisualPaintDesc());
    public bool Paint(in VisualPaintDesc desc)
    {
        if (!float.IsFinite(desc.PixelRatio) || desc.PixelRatio <= 0 || !float.IsFinite(desc.AaRadius) || desc.AaRadius < 0) return false;
        var context = new PaintContext(desc.PixelRatio, desc.ScaleOffsetOnly, desc.AaRadius);
        _root?.Paint(context);
        _paintedRoot = context.BatchRoot();
        _paintedLogicSize = _root?.Size() ?? Sizef.Zero(); _hasPainted = true;
        return true;
    }
    public bool Render(in VisualRenderDesc desc)
    {
        if (!_hasPainted || desc.Target == null || !ReferenceEquals(desc.Target.VisualBackend(), _visualBackend)
            || desc.Target.PixelSize().Width <= 0 || desc.Target.PixelSize().Height <= 0 || !desc.ClearColor.IsFinite()) return false;
        _vertices.Clear(); _indices.Clear();
        if (_paintedRoot != null)
        {
            FinalizeBatchRoot(_paintedRoot, _batchRules);
            if (!CombineBatchRoot(_paintedRoot, _vertices, _indices)) return false;
        }
        if (!_visualBackend.BeginFrame()) return false;
        _visualBackend.UpdateVertexBuffer(CollectionsMarshal.AsSpan(_vertices));
        _visualBackend.UpdateIndexBuffer(CollectionsMarshal.AsSpan(_indices));
        var viewport = desc.Viewport.IsEmpty() ? Recti.OffsetSize(Offseti.Zero(), desc.Target.PixelSize()) : desc.Viewport;
        _visualBackend.BeginRenderTarget(new VisualRenderTargetDesc { Target = desc.Target, LogicSize = _paintedLogicSize,
            Viewport = viewport, Scissor = desc.Scissor, Clear = desc.Clear, ClearColor = desc.ClearColor });
        bool dispatched = _paintedRoot == null || DispatchBatchRoot(_visualBackend, _textServices, _paintedRoot);
        _visualBackend.EndRenderTarget();
        bool submitted = _visualBackend.EndFrame(); return dispatched && submitted;
    }
    public void ClearPaint() { _paintedRoot = null; _paintedLogicSize = Sizef.Zero(); _hasPainted = false; }

    private static void FinalizeBatchRoot(BatchRoot root, IReadOnlyList<BatchRule> rules)
    {
        foreach (var command in root.Cmds)
            if (command is BatchCmdClip { SubRoot: not null } clip) FinalizeBatchRoot(clip.SubRoot, rules);
        root.ResortBatch(rules);
    }
    private static Offsetf TransformPoint(Offsetf point, Matrix4x4 transform)
    {
        var mapped = Vector4.Transform(new Vector4(point.X, point.Y, 0, 1), transform);
        return mapped.W != 0 && mapped.W != 1 ? new(mapped.X / mapped.W, mapped.Y / mapped.W) : new(mapped.X, mapped.Y);
    }
    private static bool AppendMesh(Mesh mesh, Matrix4x4 transform, List<MeshVertex> vertices, List<uint> indices, out ulong indexStart, out ulong indexEnd)
    {
        indexStart = indexEnd = (ulong)indices.Count;
        if (mesh.Vertices.Count == 0 || mesh.Indices.Count == 0) return true;
        ulong vertexBase = (ulong)vertices.Count;
        if (vertexBase + (ulong)mesh.Vertices.Count > uint.MaxValue) return false;
        vertices.EnsureCapacity(vertices.Count + mesh.Vertices.Count);
        foreach (var source in mesh.Vertices) vertices.Add(new(TransformPoint(source.Pos, transform), source.Uv, source.PackedColor));
        indices.EnsureCapacity(indices.Count + mesh.Indices.Count);
        foreach (uint source in mesh.Indices)
        {
            if (source >= mesh.Vertices.Count) return false;
            indices.Add((uint)vertexBase + source);
        }
        indexEnd = (ulong)indices.Count; return true;
    }
    private static bool CommandMesh(BatchCmd command, out Mesh? mesh, out Matrix4x4 transform)
    {
        (mesh, transform) = command switch
        {
            BatchCmdDrawMesh c => (c.Mesh, c.Transform), BatchCmdText c => (c.Mesh, c.Transform),
            BatchCmdAtlas c => (c.Mesh, c.Transform), BatchCmdClipMesh c => (c.Mesh, c.Transform),
            BatchCmdBackdrop c => (c.Mesh, c.Transform), _ => (null, default)
        };
        return command is BatchCmdDrawMesh or BatchCmdText or BatchCmdAtlas or BatchCmdClipMesh or BatchCmdBackdrop or BatchCmdClipRect;
    }
    private static bool CombineBatchRoot(BatchRoot root, List<MeshVertex> vertices, List<uint> indices)
    {
        foreach (ref var batch in CollectionsMarshal.AsSpan(root.Batches))
        {
            batch.DrawIndexStart = (ulong)indices.Count;
            ulong sortedEnd = batch.SortedCmdStart + batch.SortedCmdCount;
            for (ulong i = batch.SortedCmdStart; i < sortedEnd; i++)
            {
                ref var sorted = ref CollectionsMarshal.AsSpan(root.SortedCmds)[(int)i]; var command = root.Cmds[(int)sorted.CommandIndex];
                if (!CommandMesh(command, out var mesh, out var transform)) return false;
                sorted.DrawIndexStart = sorted.DrawIndexEnd = (ulong)indices.Count;
                if (mesh != null && !AppendMesh(mesh, transform, vertices, indices, out sorted.DrawIndexStart, out sorted.DrawIndexEnd)) return false;
            }
            batch.DrawIndexEnd = (ulong)indices.Count;
            for (ulong i = batch.SortedCmdStart; i < sortedEnd; i++)
                if (root.Cmds[(int)root.SortedCmds[(int)i].CommandIndex] is BatchCmdClip { SubRoot: not null } clip
                    && !CombineBatchRoot(clip.SubRoot, vertices, indices)) return false;
        }
        return true;
    }
    private static VisualDrawRange DrawRange(BatchRegion batch) => new() { IndexStart = batch.DrawIndexStart, IndexCount = batch.DrawIndexEnd - batch.DrawIndexStart };
    private static bool DispatchBatchRoot(VisualBackend backend, TextServices textServices, BatchRoot root)
    {
        foreach (var batch in root.Batches)
        {
            if (batch.SortedCmdCount == 0) throw new InvalidOperationException("A batch region must contain a command");
            var first = root.Cmds[(int)root.SortedCmds[(int)batch.SortedCmdStart].CommandIndex];
            switch (batch.Kind)
            {
                case EBatchRegionKind.Mesh:
                    var mesh = (BatchCmdDrawMesh)first;
                    backend.DrawMesh(new() { Range = DrawRange(batch), Texture = mesh.Texture, Shader = mesh.Shader }); break;
                case EBatchRegionKind.Text:
                    var text = (BatchCmdText)first;
                    backend.DrawText(new() { Range = DrawRange(batch), TextServices = textServices, AtlasIndex = text.AtlasIndex, PixelMode = text.PixelMode }); break;
                case EBatchRegionKind.Atlas:
                    var atlas = (BatchCmdAtlas)first;
                    backend.DrawAtlas(new() { Range = DrawRange(batch), TextServices = textServices, AtlasIndex = atlas.AtlasIndex }); break;
                case EBatchRegionKind.ClipRect:
                case EBatchRegionKind.ClipMesh:
                    var clip = (BatchCmdClip)first;
                    if (batch.SortedCmdCount != 1) throw new InvalidOperationException("Clip batches contain one clip command");
                    backend.BeginClip(new() { Kind = batch.Kind == EBatchRegionKind.ClipRect ? EVisualClipKind.Rect : EVisualClipKind.Mesh, Bound = clip.Bound, Range = DrawRange(batch) });
                    bool success = clip.SubRoot == null || DispatchBatchRoot(backend, textServices, clip.SubRoot);
                    backend.EndClip(); if (!success) return false; break;
                case EBatchRegionKind.Backdrop:
                    var backdrop = (BatchCmdBackdrop)first;
                    if (batch.SortedCmdCount != 1) throw new InvalidOperationException("Backdrop batches contain one command");
                    backend.DrawBackdrop(new() { Range = DrawRange(batch), Bound = backdrop.Bound, Command = backdrop }); break;
                default: return false;
            }
        }
        return true;
    }
}
