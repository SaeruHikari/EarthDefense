namespace SkrGui;

// Source: SkrGuiCore/math/shape/shape_sampler.hpp. Sampling caches retain source value-copy semantics; callbacks receive a readonly-value snapshot.
public struct CircleSampler
{
    private float _angle, _cosAngle = 1, _sinAngle;
    public CircleSampler() { }
    public CircleSampler(float angle) => SetAngle(angle);
    public float Angle() => _angle;
    public float CosAngle() => _cosAngle;
    public float SinAngle() => _sinAngle;
    public void SetAngle(float value) { if (_angle == value) return; _angle = value; _cosAngle = MathF.Cos(_angle); _sinAngle = MathF.Sin(_angle); }
    public Offsetf SamplePoint(Offsetf center, float radius) => center + new Offsetf(_cosAngle * radius, _sinAngle * radius);
}
public struct EllipseSampler
{
    private float _angle, _rotation, _cosAngle = 1, _sinAngle, _cosRotation = 1, _sinRotation;
    public EllipseSampler() { }
    public EllipseSampler(float angle, float rotation) { SetAngle(angle); SetRotation(rotation); }
    public float Angle() => _angle;
    public float Rotation() => _rotation;
    public float CosAngle() => _cosAngle;
    public float SinAngle() => _sinAngle;
    public float CosRotation() => _cosRotation;
    public float SinRotation() => _sinRotation;
    public void SetAngle(float value) { if (_angle == value) return; _angle = value; _cosAngle = MathF.Cos(_angle); _sinAngle = MathF.Sin(_angle); }
    public void SetRotation(float value) { if (_rotation == value) return; _rotation = value; _cosRotation = MathF.Cos(_rotation); _sinRotation = MathF.Sin(_rotation); }
    public Offsetf SamplePoint(Offsetf center, float radiusX, float radiusY)
    {
        float x = radiusX * _cosAngle, y = radiusY * _sinAngle;
        return center + new Offsetf(x * _cosRotation - y * _sinRotation, x * _sinRotation + y * _cosRotation);
    }
}
public struct SuperellipseSampler
{
    private float _angle, _rotation, _exponent = 2, _cosAngle = 1, _sinAngle, _poweredCosAngle = 1, _poweredSinAngle, _cosRotation = 1, _sinRotation;
    public SuperellipseSampler() { }
    public SuperellipseSampler(float angle, float rotation, float exponent) { SetAngle(angle); SetRotation(rotation); SetExponent(exponent); }
    public float Angle() => _angle;
    public float Rotation() => _rotation;
    public float Exponent() => _exponent;
    public float CosAngle() => _cosAngle;
    public float SinAngle() => _sinAngle;
    public float PoweredCosAngle() => _poweredCosAngle;
    public float PoweredSinAngle() => _poweredSinAngle;
    public float CosRotation() => _cosRotation;
    public float SinRotation() => _sinRotation;
    public void SetAngle(float value) { if (_angle == value) return; _angle = value; RefreshAngle(); RefreshPower(); }
    public void SetRotation(float value) { if (_rotation == value) return; _rotation = value; _cosRotation = MathF.Cos(_rotation); _sinRotation = MathF.Sin(_rotation); }
    public void SetExponent(float value) { if (_exponent == value) return; _exponent = value; RefreshPower(); }
    public Offsetf SamplePoint(Offsetf center, float radiusX, float radiusY)
    {
        float x = radiusX * _poweredCosAngle, y = radiusY * _poweredSinAngle;
        return center + new Offsetf(x * _cosRotation - y * _sinRotation, x * _sinRotation + y * _cosRotation);
    }
    private static float CleanTrigValue(float value)
    {
        if (MathF.Abs(value) <= 1e-6f) return 0; if (MathF.Abs(value - 1) <= 1e-6f) return 1; if (MathF.Abs(value + 1) <= 1e-6f) return -1; return value;
    }
    private static float SignedPow(float value, float power) { float magnitude = MathF.Pow(MathF.Abs(value), power); return value < 0 ? -magnitude : magnitude; }
    private void RefreshAngle() { _cosAngle = CleanTrigValue(MathF.Cos(_angle)); _sinAngle = CleanTrigValue(MathF.Sin(_angle)); }
    private void RefreshPower() { float power = 2 / _exponent; _poweredCosAngle = SignedPow(_cosAngle, power); _poweredSinAngle = SignedPow(_sinAngle, power); }
}
