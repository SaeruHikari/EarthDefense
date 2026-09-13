using Godot;
using Earthward.Domain;
using System.Threading.Tasks;

namespace Earthward.Rendering;

public sealed partial class FleetRenderer
{
    private readonly record struct RenderPose(bool Valid, Transform3D Transform, Color State, string? Visual, bool InSpace, bool Critical);
    private RenderPose[] _renderPoses = [];
    private IReadOnlyList<DataMap> _poseActors = Array.Empty<DataMap>();
    private string _poseCategory = "";
    private float _poseScale;
    private double _poseTime;
    private long _poseExcluded;
    private Basis _poseInverse;
    private Action<int>? _poseWorker;
    private readonly ParallelOptions _poseOptions = new() { MaxDegreeOfParallelism = Math.Max(1, Math.Min(8, System.Environment.ProcessorCount / 2)) };
    public bool ForceSerialPoses { get; set; }

    private void PrepareRenderPoses(string category, IReadOnlyList<DataMap> actors, float scale, double time, long excluded, Basis inverse)
    {
        if (_renderPoses.Length < actors.Count) Array.Resize(ref _renderPoses, Math.Max(actors.Count, _renderPoses.Length * 2));
        // The scene thread has already captured the only native value (surface basis).
        // Workers read managed records and compute value structs. Bucket mutation,
        // model loading and all RenderingServer uploads remain on the main thread.
        _poseActors = actors; _poseCategory = category; _poseScale = scale;
        _poseTime = time; _poseExcluded = excluded; _poseInverse = inverse;
        if (!ForceSerialPoses && actors.Count >= 2048 && _poseOptions.MaxDegreeOfParallelism > 1)
        {
            _poseWorker ??= BuildPoseChunk;
            Parallel.For(0, (actors.Count + 255) / 256, _poseOptions, _poseWorker);
        }
        else
            for (int i = 0; i < actors.Count; i++) _renderPoses[i] = BuildPose(actors[i], i, category, scale, time, excluded, inverse);
    }
    private void BuildPoseChunk(int chunk)
    {
        int end = Math.Min(_poseActors.Count, (chunk + 1) * 256);
        for (int i = chunk * 256; i < end; i++) _renderPoses[i] = BuildPose(_poseActors[i], i, _poseCategory, _poseScale, _poseTime, _poseExcluded, _poseInverse);
    }
    private static RenderPose BuildPose(DataMap actor, int i, string category, float scale, double time, long excludedUid, Basis inverse)
    {
        bool enemy = category == "enemies", projectile = category == "projectiles";

            long uid = actor.L("uid", i);
            if (category == "drones" && uid == excludedUid)
                return default;
            bool inSpace = actor.ContainsKey("space_position");
            var position = actor.Vector3("space_position");
            var normal = actor.Vector3("normal", Vector3.Forward);
            if (!position.IsFinite() || (!inSpace && (!normal.IsFinite() || normal.LengthSquared() < .0001f)))
                return default;
            if (!inSpace)
            {
                normal = normal.Normalized();
                position = normal * (WorldScale.EarthRadius + (float)actor.N("altitude", projectile ? .12 : .14));
            }
            var aim = actor.Vector3("aim_direction");
            bool hasAim = category == "drones" && aim.IsFinite() && aim.LengthSquared() > .0001f;
            var tangent = hasAim ? aim : actor.Vector3("tangent", Vector3.Right);
            if (!tangent.IsFinite())
                tangent = Vector3.Right;
            if (hasAim)
            {
                normal = actor.Vector3("aim_up", position.Normalized());
                if (!inSpace)
                {
                    tangent = inverse * tangent;
                    normal = inverse * normal;
                }
                tangent = tangent.Normalized();
                normal = ValidUp(tangent, normal, .999f);
            }
            else if (inSpace)
            {
                if (tangent.LengthSquared() < .0001f)
                    tangent = Vector3.Left;
                tangent = tangent.Normalized();
                normal = category == "drones" ? actor.Vector3("world_up", position.Normalized()) : Vector3.Back;
                normal = ValidUp(tangent, normal, .98f);
            }
            else
            {
                tangent -= normal * tangent.Dot(normal);
                if (tangent.LengthSquared() < .0001f)
                {
                    tangent = normal.Cross(Vector3.Up);
                    if (tangent.LengthSquared() < .0001f)
                        tangent = normal.Cross(Vector3.Right);
                }
            }
            tangent = tangent.Normalized();
            var right = tangent.Cross(normal).Normalized();
            float unitScale = scale * (category == "drones" ? FrameScales.GetValueOrDefault(actor.S("airframe_id"), 1) : 1);
            var transform = new Transform3D(new Basis(right, normal, -tangent).Scaled(Vector3.One * unitScale), position);
            double hp = actor.N("hp", 1), max = actor.N("max_hp", 1);
            bool critical = !projectile && max > 0 && hp > 0 && hp <= max * .3 && (category == "drones" || actor.S("kind") == "scout") && !actor.B("post_carrier");
            string visual = VisualKey(actor, enemy, projectile);
            float energy = Mathf.Clamp((float)(actor.N("energy_hp") / Math.Max(1, actor.N("energy_max_hp", 1))), 0, 1);
            float charge = Mathf.Clamp((float)actor.N("weapon_charge", actor.N("fire_charge", actor.N("warmup_progress"))), 0, 1);
            return new RenderPose(true, transform, new Color(energy, charge, (float)((uid * .61803398875) % 1), 1), visual, inSpace, critical);
    }
}
