using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Earthward.Application;
namespace Earthward.Presentation;

public partial class CombatIntelHud : Node2D
{
    public Main App = null!;

    private DataMap _target = new();

    private Vector2 _lastMouse = new(-10000, -10000);
    private double _pickTimer;

    public override void _Ready()
    {
        Name = "CombatIntelHUD";
        ZIndex = 24;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(App) || App.Battle == null || !IsInstanceValid(App.Planet))
            return;
        var point = App.GetGlobalMousePosition();
        if (App.Modal != "" || App.IsSpectating() || App.IsObserving() || App.Dragging || App.SelectedBuild != "" || App.IsOverUi(point))
        {
            if (_target.Count > 0)
            {
                _target = new();
                QueueRedraw();
            }
            _lastMouse = new(-10000, -10000);
            return;
        }
        _pickTimer -= delta;
        if (_pickTimer <= 0 && (point.DistanceSquaredTo(_lastMouse) >= 1 || _target.Count > 0))
        {
            _pickTimer = .14;
            _lastMouse = point;
            Pick(point);
            QueueRedraw();
        }
    }

    private void Pick(Vector2 point)
    {
        _target = new();
        Vector3 origin = App.Planet.Camera.GlobalPosition, ray = App.Planet.ProjectViewRay(point);
        float pixels = 2 * Mathf.Tan(Mathf.DegToRad(App.Planet.Camera.Fov) * .5f) / Math.Max(1, App.WorldSize.Y), best = float.PositiveInfinity;
        foreach (var enemy in App.Battle.Enemies)
        {
            if (enemy.N("hp") <= 0 || !enemy.ContainsKey("space_position"))
                continue;
            var position = enemy.Vector3("space_position");
            float depth = (position - origin).Dot(ray);
            if (depth <= 0 || depth >= best)
                continue;
            float radius = Math.Max((float)enemy.N("hit_radius", .05), depth * pixels * 6);
            if (position.DistanceSquaredTo(origin + ray * depth) > radius * radius || !App.Planet.IsSpaceVisible(position))
                continue;
            best = depth;
            _target = enemy;
        }
    }

    public static DataMap InspectEnemy(DataMap enemy)
    {
        double energy = Math.Max(0, enemy.N("energy_hp"));
        string armor = energy > 0 ? "energy" : enemy.S("body_armor", enemy.S("armor_type", "legacy"));
        string name = armor switch
        {
            "light" => "轻甲",
            "heavy" => "重甲",
            "energy" => "能量甲",
            _ => "通用船体"
        };
        string factor = armor switch
        {
            "light" => "动能 ×1.60   导弹 ×0.65   激光 ×0.65",
            "heavy" => "动能 ×0.20   导弹 ×1.65   激光 ×0.65",
            "energy" => "动能 无效   导弹 ×0.20   激光 ×1.80",
            _ => "动能 ×1.00   导弹 ×1.00   激光 ×1.00"
        };
        return new()
        {
            ["name"] = enemy.S("name", enemy.S("kind") switch
            {
                "scout" => "外星战机",
                "cruiser" => "外星重舰",
                "boss" => "外星指挥舰",
                "small_boss" => "资源运载虫",
                "meteor" => "陨石",
                _ => "外星单位"
            }),
            ["armor"] = armor,
            ["armor_name"] = name,
            ["energy"] = energy,
            ["hp"] = enemy.N("hp"),
            ["max_hp"] = enemy.N("max_hp"),
            ["factors"] = factor
        };
    }

    public override void _Draw()
    {
        if (_target.Count == 0 || !IsInstanceValid(App) || _target.N("hp") <= 0)
            return;
        var point = App.GetGlobalMousePosition();
        if (App.IsOverUi(point) || App.Modal != "" || App.Dragging)
            return;
        var info = InspectEnemy(_target);
        Color color = new(info.S("armor") == "energy" ? "c4a0f0" : info.S("armor") == "heavy" ? "eab27a" : "94d6db");
        Vector2 center = App.Planet.GetSpaceScreenPosition(_target.Vector3("space_position"));
        foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            {
                var corner = center + new Vector2(x, y) * 11;
                DrawLine(corner, corner - new Vector2(x * 4, 0), color, 1.2f, true);
                DrawLine(corner, corner - new Vector2(0, y * 4), color, 1.2f, true);
            }
        Vector2 pos = new(Math.Clamp(point.X + 22, 12, App.WorldSize.X - 342), Math.Clamp(point.Y + 22, 55, App.WorldSize.Y - 112));
        DrawRect(new Rect2(pos, new Vector2(330, 88)), new Color(.025f, .055f, .075f, .95f));
        DrawLine(pos, pos + new Vector2(330, 0), UiTheme.Alpha(color, .6f), 1, true);
        DrawString(UiTheme.Font, pos + new Vector2(12, 23), info.S("name") + "  ·  " + info.S("armor_name"), HorizontalAlignment.Left, 306, 14, color);
        string durability = $"船体 {UiTheme.Number(info.N("hp"))} / {UiTheme.Number(info.N("max_hp"))}";
        if (info.N("energy") > 0)
            durability = $"能量层 {UiTheme.Number(info.N("energy"))}  ·  " + durability;
        DrawString(UiTheme.Font, pos + new Vector2(12, 45), durability, HorizontalAlignment.Left, 306, 12, new Color("dbe7e9"));
        DrawString(UiTheme.Font, pos + new Vector2(12, 69), info.S("factors"), HorizontalAlignment.Left, 306, 11, new Color("a3b5bf"));
    }
}

