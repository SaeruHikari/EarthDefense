using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private static double StoredEarthRadius(DataMap snapshot) => snapshot.I("version") >= 3 ? snapshot.N("earth_radius") : CombatScale.LegacyEarthRadius;

    /// <summary>Canonicalize a supported save without advancing the simulation or changing the input record.</summary>
    public static DataMap? NormalizeCombatSnapshot(DataMap snapshot)
    {
        if (!ValidateCombatSnapshot(snapshot))
            return null;
        // v1 lacks a full wave plan. Its normal restore path needs the owning Game.Wave
        // and boss settings; keep it tagged as legacy here instead of guessing those values.
        if (snapshot.I("version") == 1)
            return snapshot.DeepClone();
        var normalized = snapshot.DeepClone();
        double delta = CombatScale.EarthRadius - StoredEarthRadius(snapshot);
        CombatSnapshotCodec.TryDecode(snapshot["payload"], out var decoded);
        var fields = (DataMap)decoded!;
        MigrateSnapshotSpace(fields, delta);
        fields.TryAdd("_local_shields", new DataMap());
        normalized["payload"] = CombatSnapshotCodec.Encode(fields);
        normalized["version"] = 4;
        normalized["earth_radius"] = CombatScale.EarthRadius;
        return ValidateCombatSnapshot(normalized) ? normalized : null;
    }

    private static Vector3 TranslateRadialPoint(Vector3 position, double delta)
    {
        double radius = position.Length();
        return radius <= .000001 ? position : C.Scale(position, (radius + delta) / radius);
    }

    private static void TranslatePoints(DataMap actor, double delta, params string[] fields)
    {
        foreach (var key in fields)
            if (actor.TryGetValue(key, out var value) && value is Vector3 position)
                actor[key] = TranslateRadialPoint(position, delta);
    }

    private static void TranslateRadii(DataMap actor, double delta, params string[] fields)
    {
        foreach (var key in fields)
            if (actor.TryGetValue(key, out var value) && CombatSnapshotCodec.IsNumber(value))
                actor[key] = DataMap.Number(value) + delta;
    }

    private static void MigrateSnapshotSpace(DataMap fields, double delta)
    {
        if (Math.Abs(delta) <= .000001)
            return;
        // These are Earth-centered points, never directions, local normals, speeds or weapon ranges.
        foreach (var actor in fields.List("_drones").Concat(fields.List("enemies")).Concat(fields.Map("_motherships").Values).OfType<DataMap>())
        {
            TranslatePoints(actor, delta, "space_position", "spawn_space", "landing_start", "target_space", "aim_point");
            TranslateRadii(actor, delta, "spawn_radius", "combat_entry_radius", "frontier_radius");
        }
        foreach (var actor in fields.List("_shots").Concat(fields.List("_hostile_shots")).OfType<DataMap>())
            TranslatePoints(actor, delta, "space_position", "aim_point");
        foreach (var beam in fields.List("_beams").OfType<DataMap>())
            TranslatePoints(beam, delta, "from_space", "to_space");
        foreach (var effect in fields.List("_bursts").Concat(fields.List("_damage_numbers")).OfType<DataMap>())
            TranslatePoints(effect, delta, "space_position");
        foreach (var factory in fields.Map("_factories").Values.OfType<DataMap>())
            TranslatePoints(factory.Map("site"), delta, "launch_position");
        TranslateRadii(fields.Map("_post_plan"), delta, "frontier_radius");
        foreach (var tower in fields.Map("_local_shields").Values.OfType<DataMap>())
        {
            TranslateRadii(tower, delta, "shield_radius");
            tower["angle_radians"] = Math.Min(Math.PI, C.N(tower, "surface_radius") / CombatScale.EarthRadius);
        }
        // Legacy cloud, stars and retired remote bases are not members of the Earth battle payload.
    }
}
