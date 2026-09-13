using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace SkrGui;

public readonly record struct Float3x3(float M00, float M01, float M02, float M10, float M11, float M12, float M20, float M21, float M22)
{
    public bool Equals(Float3x3 other) => M00 == other.M00 && M01 == other.M01 && M02 == other.M02 && M10 == other.M10 && M11 == other.M11 && M12 == other.M12 && M20 == other.M20 && M21 == other.M21 && M22 == other.M22;
    public override int GetHashCode() { var hash = new HashCode(); hash.Add(M00); hash.Add(M01); hash.Add(M02); hash.Add(M10); hash.Add(M11); hash.Add(M12); hash.Add(M20); hash.Add(M21); hash.Add(M22); return hash.ToHashCode(); }
    public static Float3x3 Identity() => new(1, 0, 0, 0, 1, 0, 0, 0, 1);
    public bool IsFinite() => float.IsFinite(M00) && float.IsFinite(M01) && float.IsFinite(M02) && float.IsFinite(M10) && float.IsFinite(M11) && float.IsFinite(M12) && float.IsFinite(M20) && float.IsFinite(M21) && float.IsFinite(M22);
}
// Source: SkrBase/math/manual/math_func/euler_utils.hpp, ZXY radians.
public struct RotatorF(float pitch,float yaw,float roll)
{
    public float Pitch=pitch,Yaw=yaw,Roll=roll;
    public Vector3 AsVector()=>new(Pitch,Yaw,Roll);
    public Quaternion ToQuat()
    {
        float sp=MathF.Sin(Pitch*.5f),cp=MathF.Cos(Pitch*.5f),sy=MathF.Sin(Yaw*.5f),cy=MathF.Cos(Yaw*.5f),sr=MathF.Sin(Roll*.5f),cr=MathF.Cos(Roll*.5f);
        return new(cr*sp*cy+sr*cp*sy,cr*cp*sy-sr*sp*cy,sr*cp*cy-cr*sp*sy,cr*cp*cy+sr*sp*sy);
    }
    public Matrix4x4 ToMatrix4()=>Matrix4x4.CreateFromQuaternion(ToQuat());
}
public struct PaintTransform2D
{
    public Offsetf Offset, Shear, Pivot;
    public float Rotation;
    public Offsetf Scale = new(1, 1);
    public PaintTransform2D() { }
}
public struct TransformF
{
    public Vector3 Position, Scale = Vector3.One;
    public Quaternion Rotation = Quaternion.Identity;
    public TransformF() { }
    public static TransformF Identity() => new();
}
public struct PaintTransform3D
{
    public TransformF Transform = TransformF.Identity();
    public Vector3 Pivot;
    public PaintTransform3D() { }
}

// Source: SkrGuiCore/math/transform/paint_transform.hpp.
// Matrix4x4 is storage for the original row-vector 4x4 matrix, including projective components.
public struct PaintTransform
{
    private Offsetf _offset;
    private float _pixelRatioScale = 1;
    private bool _hasTransform, _scaleOffsetOnly = true;
    private Matrix4x4 _transform = Matrix4x4.Identity;
    public PaintTransform() { }
    [UnscopedRef] public ref readonly Offsetf Offset() => ref _offset;
    public float PixelRatioScale() => _pixelRatioScale;
    public bool HasTransform() => _hasTransform;
    public bool ScaleOffsetOnly() => _scaleOffsetOnly;
    [UnscopedRef] public ref readonly Matrix4x4 Transform() => ref _transform;
    public Matrix4x4 ResolvedTransform() => _hasTransform ? _transform : TranslationMatrix(_offset);
    public void Reset() { _offset = default; _pixelRatioScale = 1; _hasTransform = false; _scaleOffsetOnly = true; _transform = Matrix4x4.Identity; }
    public Offsetf TransformPoint(Offsetf point) => !_hasTransform ? point + _offset : ProjectPoint(Vector4.Transform(new Vector4(point.X, point.Y, 0, 1), _transform));
    public Offsetf TransformVector(Offsetf vector)
    {
        if (!_hasTransform) return vector;
        var result = Vector4.Transform(new Vector4(vector.X, vector.Y, 0, 0), _transform); return new Offsetf(result.X, result.Y);
    }
    public Rectf TransformRect(Rectf rect)
    {
        if (rect == Rectf.Largest()) return rect; if (!_hasTransform) return rect.Shift(_offset);
        var p0 = TransformPoint(rect.TopLeft()); var p1 = TransformPoint(rect.TopRight()); var p2 = TransformPoint(rect.BottomRight()); var p3 = TransformPoint(rect.BottomLeft());
        return Rectf.Points(p0, p1).Hold(p2).Hold(p3);
    }
    public void ApplyOffset2D(Offsetf offset)
    {
        Debug.Assert(offset.IsFinite()); if (IsZero(offset)) return;
        if (!_hasTransform) { _offset += offset; return; } AppendTransform(TranslationMatrix(offset));
    }
    public void ApplyScale2D(Offsetf scale)
    {
        Debug.Assert(scale.IsFinite()); if (IsIdentityScale(scale)) return;
        _pixelRatioScale *= CppMath.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)); AppendTransform(Matrix4x4.CreateScale(scale.X, scale.Y, 1));
    }
    public void ApplyRotation2D(float radians)
    {
        Debug.Assert(float.IsFinite(radians)); if (radians == 0) return;
        _scaleOffsetOnly = false; AppendTransform(Matrix4x4.CreateRotationZ(radians));
    }
    public void ApplyShear2D(Offsetf shear)
    {
        Debug.Assert(shear.IsFinite()); if (IsZero(shear)) return;
        _pixelRatioScale *= MaxSingularValue2D(1, shear.Y, shear.X, 1); _scaleOffsetOnly = false;
        AppendTransform(new Matrix4x4(1, shear.Y, 0, 0, shear.X, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1));
    }
    public void ApplyOffset3D(Vector3 offset)
    {
        Debug.Assert(IsFinite(offset)); if (offset == Vector3.Zero) return;
        if (offset.Z == 0 && !_hasTransform) { _offset += new Offsetf(offset.X, offset.Y); return; }
        if (offset.Z != 0) _scaleOffsetOnly = false; AppendTransform(Matrix4x4.CreateTranslation(offset));
    }
    public void ApplyScale3D(Vector3 scale)
    {
        Debug.Assert(IsFinite(scale)); if (scale == Vector3.One) return;
        _scaleOffsetOnly = false; AppendTransform(Matrix4x4.CreateScale(scale));
    }
    public void ApplyRotation3D(RotatorF rotation)
    {
        Debug.Assert(IsFinite(rotation.AsVector()));if(rotation.Pitch==0&&rotation.Yaw==0&&rotation.Roll==0)return;
        _scaleOffsetOnly=false;AppendTransform(rotation.ToMatrix4());
    }
    public void ApplyRotation3D(Quaternion rotation)
    {
        Debug.Assert(float.IsFinite(rotation.X) && float.IsFinite(rotation.Y) && float.IsFinite(rotation.Z) && float.IsFinite(rotation.W));
        if (rotation == Quaternion.Identity) return; _scaleOffsetOnly = false; AppendTransform(Matrix4x4.CreateFromQuaternion(rotation));
    }
    public void ApplyPerspective(float viewWidth, float viewHeight, float nearDistance, float farDistance)
    {
        Debug.Assert(float.IsFinite(viewWidth) && float.IsFinite(viewHeight) && float.IsFinite(nearDistance) && float.IsFinite(farDistance));
        float doubleNear = nearDistance + nearDistance, scaledFar = farDistance / (farDistance - nearDistance);
        _scaleOffsetOnly = false;
        AppendTransform(new Matrix4x4(doubleNear / viewWidth, 0, 0, 0, 0, doubleNear / viewHeight, 0, 0, 0, 0, scaledFar, 1, 0, 0, -scaledFar * nearDistance, 0));
    }
    public void ApplyPerspectiveFov(float fovV, float aspectRatio, float nearDistance, float farDistance)
    {
        Debug.Assert(float.IsFinite(fovV) && float.IsFinite(aspectRatio) && float.IsFinite(nearDistance) && float.IsFinite(farDistance));
        float s = MathF.Sin(fovV * .5f), c = MathF.Cos(fovV * .5f), height = c / s, width = height / aspectRatio, far = farDistance / (farDistance - nearDistance);
        _scaleOffsetOnly = false; AppendTransform(new Matrix4x4(width, 0, 0, 0, 0, height, 0, 0, 0, 0, far, 1, 0, 0, -far * nearDistance, 0));
    }
    public void Apply(Float3x3 transform)
    {
        Debug.Assert(transform.IsFinite()); if (transform == Float3x3.Identity()) return;
        _pixelRatioScale *= MaxSingularValue2D(transform.M00, transform.M01, transform.M10, transform.M11);
        if (!(transform.M01 == 0 && transform.M02 == 0 && transform.M10 == 0 && transform.M12 == 0 && transform.M22 == 1)) _scaleOffsetOnly = false;
        if (transform.M00 == 1 && transform.M01 == 0 && transform.M02 == 0 && transform.M10 == 0 && transform.M11 == 1 && transform.M12 == 0 && transform.M22 == 1) { ApplyOffset2D(new Offsetf(transform.M20, transform.M21)); return; }
        AppendTransform(new Matrix4x4(transform.M00, transform.M01, 0, transform.M02, transform.M10, transform.M11, 0, transform.M12, 0, 0, 1, 0, transform.M20, transform.M21, 0, transform.M22));
    }
    public void Apply(Matrix4x4 transform)
    {
        Debug.Assert(IsFinite(transform)); if (transform == Matrix4x4.Identity) return;
        _scaleOffsetOnly = false; AppendTransform(transform);
    }
    public void Apply(PaintTransform transform)
    {
        if (!transform._hasTransform) { ApplyOffset2D(transform._offset); return; }
        float pixelRatioScale = transform._pixelRatioScale; bool scaleOffsetOnly = transform._scaleOffsetOnly; Matrix4x4 localTransform = transform._transform;
        _pixelRatioScale *= pixelRatioScale; if (!scaleOffsetOnly) _scaleOffsetOnly = false; AppendTransform(localTransform);
    }
    public void Apply(PaintTransform2D transform)
    {
        bool hasScale = !IsIdentityScale(transform.Scale), hasShear = !IsZero(transform.Shear), hasRotation = transform.Rotation != 0, hasPivot = !IsZero(transform.Pivot);
        if (transform.Offset != Offsetf.Zero()) ApplyOffset2D(transform.Offset);
        if (hasScale || hasShear || hasRotation)
        {
            if (hasPivot) ApplyOffset2D(transform.Pivot); if (hasRotation) ApplyRotation2D(transform.Rotation);
            if (hasShear) ApplyShear2D(transform.Shear); if (hasScale) ApplyScale2D(transform.Scale); if (hasPivot) ApplyOffset2D(-transform.Pivot);
        }
    }
    public void Apply(PaintTransform3D transform)
    {
        bool hasPosition = transform.Transform.Position != Vector3.Zero, hasScale = transform.Transform.Scale != Vector3.One, hasRotation = transform.Transform.Rotation != Quaternion.Identity, hasPivot = transform.Pivot != Vector3.Zero;
        if (hasPosition) ApplyOffset3D(transform.Transform.Position);
        if (hasScale || hasRotation)
        {
            if (hasPivot) ApplyOffset3D(transform.Pivot); if (hasRotation) ApplyRotation3D(transform.Transform.Rotation);
            if (hasScale) ApplyScale3D(transform.Transform.Scale); if (hasPivot) ApplyOffset3D(-transform.Pivot);
        }
    }
    private static Offsetf ProjectPoint(Vector4 point)
    {
        if (float.IsFinite(point.W) && MathF.Abs(point.W) > 1e-10f) { point.X /= point.W; point.Y /= point.W; }
        return new Offsetf(point.X, point.Y);
    }
    private static Matrix4x4 TranslationMatrix(Offsetf offset) => Matrix4x4.CreateTranslation(offset.X, offset.Y, 0);
    private static bool IsZero(Offsetf value) => value.X == 0 && value.Y == 0;
    private static bool IsIdentityScale(Offsetf value) => value.X == 1 && value.Y == 1;
    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool IsFinite(Matrix4x4 v) => float.IsFinite(v.M11) && float.IsFinite(v.M12) && float.IsFinite(v.M13) && float.IsFinite(v.M14) && float.IsFinite(v.M21) && float.IsFinite(v.M22) && float.IsFinite(v.M23) && float.IsFinite(v.M24) && float.IsFinite(v.M31) && float.IsFinite(v.M32) && float.IsFinite(v.M33) && float.IsFinite(v.M34) && float.IsFinite(v.M41) && float.IsFinite(v.M42) && float.IsFinite(v.M43) && float.IsFinite(v.M44);
    private static float MaxSingularValue2D(float m00, float m01, float m10, float m11)
    {
        float frobeniusSquared = m00 * m00 + m01 * m01 + m10 * m10 + m11 * m11, determinant = m00 * m11 - m01 * m10;
        float discriminant = CppMath.Max(0, frobeniusSquared * frobeniusSquared - 4 * determinant * determinant);
        return MathF.Sqrt(.5f * (frobeniusSquared + MathF.Sqrt(discriminant)));
    }
    private void MaterializeOffset()
    {
        if (_hasTransform) return; _transform = TranslationMatrix(_offset); _offset = Offsetf.Zero(); _hasTransform = true;
    }
    private void AppendTransform(Matrix4x4 localTransform) { MaterializeOffset(); _transform = localTransform * _transform; }
}
