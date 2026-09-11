using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Earthward.Application;
namespace Earthward.Presentation;

public partial class CombatHud : Node2D
{
    public Main App = null!;
    public double ShieldFlash
    {
        get; set;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(App))
            return;
        if (!App.WorldIsPaused())
        {
            ShieldFlash = Math.Max(0, ShieldFlash - delta * 1.4);
        }
        QueueRedraw();
    }

    public double LastDrawMs { get; private set; }
    public override void _Draw()
    {
        if (App?.CaptureFrameTimings != true) { DrawCombat(); return; }
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        DrawCombat();
        LastDrawMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }
    private void DrawCombat()
    {
        if (!IsInstanceValid(App) || App.Battle == null || !IsInstanceValid(App.Planet) || App.IsObserving())
            return;
        var planet = App.Planet;
        var battle = App.Battle;
        foreach (var enemy in battle.Enemies)
        {
            var p = enemy.Vector3("space_position");
            if (!planet.IsSpaceVisible(p))
                continue;
            Color c = UiTheme.Coral;
            var tip = planet.GetSpaceScreenPosition(p);
            var tail = planet.GetSpaceScreenPosition(p - enemy.Vector3("tangent") * .4f);
            DrawLine(tail, tip, UiTheme.Alpha(c, .16f), 1, true);
            if (enemy.N("age") < .8)
                DrawArc(tip, 11 + (float)enemy.N("age") * 9, 0, Mathf.Tau, 32, UiTheme.Alpha(c, (float)(.8 - enemy.N("age")) * .5f), 1, true);
        }
        foreach (var unit in battle.Motherships.Values)
            DrawCommander(unit);
        foreach (var unit in battle.Enemies)
            if (unit.S("kind") is "boss" or "small_boss" || unit.B("post_carrier"))
                DrawCommander(unit);
        foreach (var number in battle.DamageNumbers)
        {
            var position = number.Vector3("space_position");
            if (!planet.IsSpaceVisible(position))
                continue;
            float age = (float)number.N("age"), t = age / Math.Max(.001f, (float)number.N("life", 1));
            var pos = planet.GetSpaceScreenPosition(position) + new Vector2((float)number.N("offset"), -12 - t * 24);
            float pop = 1 + .24f * Math.Max(0, 1 - age / .16f);
            string text = DamageText(number.N("amount"));
            Color color = ReadColor(number, "color", UiTheme.Cyan);
            color.A = Math.Clamp((1 - t) * 2.1f, 0, 1);
            float width = UiTheme.Font.GetStringSize(text, fontSize: 18).X;
            DrawSetTransform(pos, 0, Vector2.One * pop);
            DrawStringOutline(UiTheme.Font, new Vector2(-width * .5f, 0), text, HorizontalAlignment.Left, -1, 18, 3, new Color(.025f, .055f, .075f, color.A * .9f));
            DrawString(UiTheme.Font, new Vector2(-width * .5f, 0), text, fontSize: 18, modulate: color);
            DrawSetTransform(Vector2.Zero);
        }
        if (!App.IsSpectating())
        {
            if (ShieldFlash > 0)
                DrawArc(App.WorldSize * .5f, planet.GetProjectedPlanetRadius() + 3, 0, Mathf.Tau, 160, new Color(.55f, .92f, .96f, (float)ShieldFlash * .32f), 1.5f, true);
        }
        DrawThreats();
    }

    private void DrawCommander(DataMap unit)
    {
        if (unit.N("hp") >= unit.N("max_hp") || !App.Planet.IsSpaceVisible(unit.Vector3("space_position")))
            return;
        var start = App.Planet.GetSpaceScreenPosition(unit.Vector3("space_position")) + new Vector2(-24, -(float)unit.N("size") - 6);
        DrawLine(start, start + new Vector2(48, 0), new Color(.1f, .16f, .2f, .8f), 3, true);
        DrawLine(start, start + new Vector2(48 * (float)(unit.N("hp") / Math.Max(.001, unit.N("max_hp"))), 0), UiTheme.Coral, 2, true);
    }

    private void DrawThreats()
    {
        if (!App.Battle.Active)
            return;
        var placed = new List<Vector2>();
        Vector2 center = App.WorldSize * .5f;
        Rect2 bounds = new(new Vector2(25, 48), App.WorldSize - new Vector2(50, 96));
        foreach (var enemy in App.Battle.Enemies)
        {
            if (enemy.S("phase") == "retreat")
                continue;
            Vector3 p = enemy.Vector3("space_position");
            if (!p.IsFinite())
                continue;
            Vector3 relative = App.Planet.GetCameraRelativePosition(p);
            bool edge;
            Vector2 direction;
            string label;
            if (relative.Z >= 0)
            {
                Vector2 bearing = new(relative.X, -relative.Y);
                direction = bearing.LengthSquared() < .00000001f ? Vector2.Down : bearing.Normalized();
                edge = true;
                label = "后方";
            }
            else
            {
                var projected = App.Planet.GetSpaceScreenPosition(p);
                if (!projected.IsFinite())
                    continue;
                edge = !bounds.HasPoint(projected);
                if (!edge && App.Planet.IsSpaceVisible(p))
                    continue;
                Vector2 bearing = projected - center;
                direction = bearing.LengthSquared() < .0001f ? Vector2.Up : bearing.Normalized();
                label = edge ? "方位" : "背面";
            }
            Vector2 hint;
            if (edge)
            {
                Vector2 half = bounds.Size * .5f;
                float distance = Math.Min(half.X / Math.Max(Math.Abs(direction.X), .001f), half.Y / Math.Max(Math.Abs(direction.Y), .001f));
                hint = center + direction * (distance - 8);
            }
            else
                hint = center + direction * (App.Planet.GetProjectedPlanetRadius() + 18);
            hint = hint.Clamp(bounds.Position, bounds.End);
            if (placed.Any(previous => previous.DistanceSquaredTo(hint) < 784))
                continue;
            placed.Add(hint);
            Vector2 side = direction.Orthogonal();
            DrawColoredPolygon(new[] { hint + direction * 5, hint - direction * 4 + side * 3.5f, hint - direction * 4 - side * 3.5f }, UiTheme.Alpha(UiTheme.Coral, .72f));
            DrawString(UiTheme.Font, hint + new Vector2(direction.X > 0 ? -31 : 9, 4), label, fontSize: 10, modulate: UiTheme.Alpha(UiTheme.Coral, .62f));
            if (placed.Count >= 10)
                break;
        }
    }

    private static Color ReadColor(DataMap map, string key, Color fallback)
    {
        if (map.Value(key) is Color c)
            return c;
        var list = map.List(key);
        return list.Count >= 3 ? new Color((float)DataMap.Number(list[0]), (float)DataMap.Number(list[1]), (float)DataMap.Number(list[2]), list.Count > 3 ? (float)DataMap.Number(list[3]) : 1) : fallback;
    }

    private static string DamageText(double amount) => Math.Abs(amount - Math.Round(amount)) < .0001 && amount >= .5 ? $"-{amount:0}" : amount < .01 ? "-<0.01" : amount < 1 ? $"-{amount:0.00}" : $"-{amount:0.0}";
}

