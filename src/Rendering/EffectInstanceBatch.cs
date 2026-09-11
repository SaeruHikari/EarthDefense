using Godot;
using System.Runtime.CompilerServices;

namespace Earthward.Rendering;

/// <summary>Reusable transform/custom-data storage with one native buffer upload per changed effect group.</summary>
internal sealed class EffectInstanceBatch
{
    private readonly MultiMeshInstance3D _node;
    private readonly MultiMesh _mesh;
    private float[] _buffer = [], _uploaded = [];
    private int _capacity, _nativeCapacity, _lastCount;
    private bool _visible;
    private Vector3 _minimum, _maximum;
    private Aabb _lastBounds;
    public int Count { get; private set; }
    public long BufferUploads { get; private set; }
    public long NativeWrites { get; private set; }
    public MultiMeshInstance3D Node => _node;

    public EffectInstanceBatch(Node3D parent, string name, Mesh geometry, Material material)
    {
        _mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = geometry };
        _node = new MultiMeshInstance3D { Name = name, Multimesh = _mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        parent.AddChild(_node);
        Begin();
    }

    public void Begin()
    {
        Count = 0;
        _minimum = Vector3.One * float.PositiveInfinity;
        _maximum = Vector3.One * float.NegativeInfinity;
    }

    /// <summary>Call once per effect/beam, rather than transforming an AABB for every spark.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncludeBounds(Vector3 minimum, Vector3 maximum)
    {
        _minimum = _minimum.Min(minimum);
        _maximum = _maximum.Max(maximum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in Transform3D transform, Color state)
    {
        if (Count == _capacity) Grow();
        // Sequential stores let the JIT remove repeated bounds checks. Compare whole buffers using SIMD at commit.
        Span<float> row = _buffer.AsSpan(Count++ * 16, 16);
        var basis = transform.Basis;
        row[0] = basis.X.X; row[1] = basis.Y.X; row[2] = basis.Z.X; row[3] = transform.Origin.X;
        row[4] = basis.X.Y; row[5] = basis.Y.Y; row[6] = basis.Z.Y; row[7] = transform.Origin.Y;
        row[8] = basis.X.Z; row[9] = basis.Y.Z; row[10] = basis.Z.Z; row[11] = transform.Origin.Z;
        row[12] = state.R; row[13] = state.G; row[14] = state.B; row[15] = state.A;
    }

    private void Grow()
    {
        _capacity = Math.Max(16, _capacity * 2);
        Array.Resize(ref _buffer, _capacity * 16);
        Array.Resize(ref _uploaded, _capacity * 16);
    }

    public void Commit()
    {
        if (Count == 0)
        {
            if (_lastCount != 0) { _mesh.VisibleInstanceCount = 0; NativeWrites++; }
            if (_visible) { _node.Visible = false; NativeWrites++; }
            _lastCount = 0;
            _visible = false;
            return;
        }
        bool resized = _nativeCapacity != _capacity;
        if (resized)
        {
            _mesh.InstanceCount = _capacity;
            _nativeCapacity = _capacity;
            NativeWrites++;
        }
        var bounds = new Aabb(_minimum - Vector3.One * .001f, _maximum - _minimum + Vector3.One * .002f);
        if (bounds != _lastBounds)
        {
            _mesh.CustomAabb = bounds;
            _lastBounds = bounds;
            NativeWrites++;
        }
        if (resized || !_buffer.AsSpan(0, Count * 16).SequenceEqual(_uploaded.AsSpan(0, Count * 16)))
        {
            _mesh.Buffer = _buffer;
            (_buffer, _uploaded) = (_uploaded, _buffer);
            BufferUploads++;
            NativeWrites++;
        }
        if (_lastCount != Count) { _mesh.VisibleInstanceCount = Count; NativeWrites++; }
        if (!_visible) { _node.Visible = true; NativeWrites++; }
        _lastCount = Count;
        _visible = true;
    }

    public void Clear()
    {
        Begin();
        Commit();
    }
}