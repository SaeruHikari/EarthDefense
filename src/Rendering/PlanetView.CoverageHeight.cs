using Godot;
using Earthward.Combat;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private MeshInstance3D? _coverageHeight;
    private FactoryCoverageSnapshot? _heightSnapshot;
    private Vector3 _heightNormal;

    private void UpdateCoverageHeight(FactoryCoverageSnapshot? coverage)
    {
        if (coverage == null || coverage.Bands.Count == 0)
        {
            if (_coverageHeight != null) _coverageHeight.Hide();
            _heightSnapshot = null;
            return;
        }
        Vector3 normal = GetSiteNormal((int)coverage.SiteId);
        if (ReferenceEquals(coverage, _heightSnapshot) && normal == _heightNormal) return;
        _heightSnapshot = coverage;
        _heightNormal = normal;
        if (_coverageHeight == null)
        {
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true, AlbedoColor = Colors.White,
                NoDepthTest = false, DisableFog = true
            };
            _coverageHeight = new MeshInstance3D { Name = "SelectedFactoryHeightEnvelope", MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            Globe.AddChild(_coverageHeight);
        }
        var points = new List<Vector3>(); var colors = new List<Color>();
        Vector3 axis = normal.Cross(Math.Abs(normal.Y) < .95 ? Vector3.Up : Vector3.Right).Normalized(), other = normal.Cross(axis).Normalized();
        var lower = new Color(.46f, .95f, .83f, .44f); var upper = new Color(.55f, .84f, 1, .78f);
        void Line(Vector3 a, Vector3 b, Color color) { points.Add(a); points.Add(b); colors.Add(color); colors.Add(color); }
        Vector3 Direction(double angle, double phase) => normal * (float)Math.Cos(angle) + (axis * (float)Math.Cos(phase) + other * (float)Math.Sin(phase)) * (float)Math.Sin(angle);
        foreach (var band in coverage.Bands.DistinctBy(b => (b.PursuitRadius, b.AngleRadians)))
        {
            float bottom = WorldScale.EarthRadius + WorldScale.DroneAltitude, top = (float)band.PursuitRadius;
            double angle = Math.Min(Math.PI, band.AngleRadians);
            if (angle < Math.PI - .001)
            {
                for (int i = 0; i < 96; i++)
                {
                    var a = Direction(angle, i * Math.Tau / 96); var b = Direction(angle, (i + 1) * Math.Tau / 96);
                    if (i % 4 < 2) Line(a * top, b * top, upper);
                    if (i % 6 < 2) Line(a * bottom, b * bottom, lower);
                }
                for (int i = 0; i < 2; i++)
                {
                    var edge = Direction(angle, i * Math.PI);
                    Line(edge * bottom, edge * top, new(.56f, .85f, 1, .50f));
                }
            }
            Line(normal * bottom, normal * top, new(.56f, .85f, 1, .58f));
            float tick = Math.Min(.13f, (top - bottom) * .07f);
            for (int i = 0; i <= 4; i++)
            {
                var center = normal * Mathf.Lerp(bottom, top, i / 4f);
                Line(center - axis * tick, center + axis * tick, upper);
            }
        }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray(); arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        var old = _coverageHeight.Mesh;
        _coverageHeight.Mesh = mesh;
        old?.Dispose();
        _coverageHeight.Show();
    }
}
