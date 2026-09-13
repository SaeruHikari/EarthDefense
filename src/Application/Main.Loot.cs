using System;
using System.Collections.Generic;
using Godot;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    private sealed record ProjectedLoot(DataMap Pickup, Vector2 At, float Radius);
    private sealed class LootPulse { public double Age, Amount; }
    private readonly List<ProjectedLoot> _projectedLoot = new();
    private readonly Dictionary<string, Texture2D> _lootIcons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LootPulse> _lootPulses = new(StringComparer.Ordinal);
    private long _hoveredLootUid = -1;
    private int _lootEpoch = -1, _lootProjectedCount = -1;
    private bool _lootTipShown;
    private double _lootProjectionAt = double.NegativeInfinity;
    public int LootFlightCount => Battle.LootPickups.Count(p => p.S("phase") == "flying");

    private static string LootName(string currency) => currency switch {
        "minerals" => "矿物", "energy" => "能量", "science" => "科研数据", "resource_cores" => "资源核心",
        "alien_points" => "外星科技点", "alien_chips" => "外星芯片", _ => currency
    };
    private static string LootIcon(string currency) => currency switch {
        "resource_cores" => "core", "alien_points" => "alien", "alien_chips" => "chip", _ => currency
    };
    private static Color LootColor(string currency) => currency switch {
        "minerals" => new("88dfd2"), "energy" => new("f4d279"), "science" => new("7acfff"),
        "resource_cores" => new("ffb966"), "alien_points" => new("c49aff"), "alien_chips" => new("88f0b6"), _ => UiTheme.Cyan
    };
    private void ResetLootFeedback()
    {
        _hoveredLootUid = -1; _lootProjectionAt = double.NegativeInfinity; _lootProjectedCount = -1; _lootTipShown = false;
        _projectedLoot.Clear(); _lootPulses.Clear(); CancelNavigationPress(); ClearNavigationFeedback();
        InvalidateHudData();
    }
    private void RefreshProjectedLoot(bool force = false)
    {
        if (!force && _lootProjectedCount == Battle.LootPickups.Count && Elapsed - _lootProjectionAt < 1d / 30) return;
        _projectedLoot.Clear(); _lootProjectionAt = Elapsed; _lootProjectedCount = Battle.LootPickups.Count;
        foreach (var pickup in Battle.LootPickups)
        {
            if (pickup.S("phase") != "world") continue;
            var position = pickup.Vector3("space_position");
            Vector2 at = Planet.GetSpaceScreenPosition(position);
            if (!new Rect2(new Vector2(-30, -30), WorldSize + new Vector2(60, 60)).HasPoint(at) || !Planet.IsSpaceVisible(position)) continue;
            _projectedLoot.Add(new(pickup, at, Battlefield.RareLoot(pickup.S("currency")) ? 25 : 20));
        }
    }
    public DataMap PickLootTarget(Vector2 point, bool precise = false)
    {
        RefreshProjectedLoot(precise);
        ProjectedLoot? selected = null;
        float best = float.PositiveInfinity;
        foreach (var item in _projectedLoot)
        {
            if (item.Pickup.S("phase") != "world") continue;
            float distance = point.DistanceSquaredTo(item.At);
            if (distance > item.Radius * item.Radius || distance >= best) continue;
            selected = item; best = distance;
        }
        return selected == null ? new() : new() { ["kind"] = "loot", ["uid"] = selected.Pickup.L("uid"),
            ["currency"] = selected.Pickup.S("currency"), ["distance"] = Math.Sqrt(best) };
    }
    public bool TryCollectLoot(long uid)
    {
        var pickup = Battle.FindLootPickup(uid);
        if (pickup == null || pickup.S("phase") != "world" || !Planet.IsSpaceVisible(pickup.Vector3("space_position"))) return false;
        Vector2 origin = Planet.GetSpaceScreenPosition(pickup.Vector3("space_position"));
        if (!new Rect2(Vector2.Zero, WorldSize).HasPoint(origin)) return false;
        double duration = .62 + Math.Clamp(origin.DistanceTo(LootHudTarget(pickup.S("currency"))) / 1800, 0, .38);
        if (!Battle.BeginLootFlight(uid, origin / WorldSize, duration)) return false;
        _hoveredLootUid = -1; _lootProjectionAt = double.NegativeInfinity;
        Sounds.PlaySound("build"); SaveIfSafe(); QueueRedraw();
        return true;
    }
    public Vector2 LootHudTarget(string currency)
    {
        var items = HeaderData();
        float factor = Math.Min(1, Math.Max(1, WorldSize.X - 204) / _headerTotalWidth), x = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].S("id") == currency) return new Vector2(16, 14) + new Vector2(x + 15, 17) * factor;
            x += _headerWidths[i] + 4;
        }
        return new(31, 31);
    }
    private Vector2 LootFlightPosition(DataMap pickup, double progress)
    {
        Vector2 start = new Vector2((float)pickup.N("screen_x"), (float)pickup.N("screen_y")) * WorldSize;
        Vector2 end = LootHudTarget(pickup.S("currency"));
        float u = (float)Math.Clamp(progress, 0, 1), t = u * (.45f + .55f * u), a = 1 - t;
        Vector2 c1 = start + new Vector2(35 + pickup.L("uid") % 3 * 15, -Math.Clamp(start.DistanceTo(end) * .2f, 50, 180));
        Vector2 c2 = end + new Vector2(70, 90);
        return a * a * a * start + 3 * a * a * t * c1 + 3 * a * t * t * c2 + t * t * t * end;
    }
    private void UpdateLootPresentation(double delta)
    {
        if (_lootEpoch != Battle.TimelineEpoch) { _lootEpoch = Battle.TimelineEpoch; ResetLootFeedback(); }
        if (!_lootTipShown && Modal == "" && !Defeated && NoticeTime <= 0 && Battle.LootPickups.Any(p => p.S("phase") == "world"))
        {
            ShowNotice("战利品已掉落 · 左键点击回收，飞抵左上角后资源入账");
            _lootTipShown = true;
        }
        foreach (var pulse in _lootPulses.Values) pulse.Age += delta;
        foreach (var key in _lootPulses.Where(p => p.Value.Age >= .6).Select(p => p.Key).ToArray()) _lootPulses.Remove(key);
        var arrivals = Battle.AdvanceLootFlights(delta);
        foreach (var arrival in arrivals)
        {
            if (!arrival.B("ok")) { ShowNotice("回收保存未完成，物品已返回原处，可再次拾取"); continue; }
            string currency = arrival.S("currency");
            if (arrival.N("amount") > 0)
            {
                if (!_lootPulses.TryGetValue(currency, out var pulse)) _lootPulses[currency] = pulse = new();
                pulse.Amount = pulse.Age < .15 ? pulse.Amount + arrival.N("amount") : arrival.N("amount"); pulse.Age = 0;
            }
            InvalidateHudData(); SaveIfSafe();
        }
        if (arrivals.Count > 0) _lootProjectionAt = double.NegativeInfinity;
    }
    private void DrawLootGlyph(string currency, Vector2 at, float size, Color color)
    {
        if (currency != "alien_chips") { HudIcon(LootIcon(currency), at, size, color); return; }
        Vector2 half = new(size * .63f, size * .53f);
        DrawRect(new(at - half, half * 2), color, false, 1.4f);
        DrawRect(new(at - half * .5f, half), new Color(color, .45f));
        for (int i = -1; i <= 1; i++)
        {
            float x = size * .35f * i;
            DrawLine(at + new Vector2(x, -half.Y), at + new Vector2(x, -size * .88f), color, 1.2f, true);
            DrawLine(at + new Vector2(x, half.Y), at + new Vector2(x, size * .88f), color, 1.2f, true);
        }
    }
    private void DrawLootHints()
    {
        if (Modal != "" || Defeated || IsSpectating()) return;
        RefreshProjectedLoot();
        foreach (var item in _projectedLoot)
        {
            var pickup = item.Pickup;
            if (pickup.S("phase") != "world") continue;
            Vector2 p = item.At;
            string currency = pickup.S("currency"); Color color = LootColor(currency);
            bool rare = Battlefield.RareLoot(currency), hover = pickup.L("uid") == _hoveredLootUid;
            float wave = (float)(.5 + .5 * Math.Sin(Elapsed * 3 + pickup.L("uid") * .37));
            float r = rare ? 18 : 13;
            DrawCircle(p, r + 6 + wave * 3, new Color(color, (rare ? .09f : .04f) + (hover ? .07f : 0)));
            DrawArc(p, r + wave * 2, (float)Elapsed * .35f, (float)Elapsed * .35f + Mathf.Tau * .8f, 22, new Color(color, hover ? 1 : rare ? .85f : .46f), hover ? 2 : 1, true);
            if (rare)
            {
                DrawLine(p + new Vector2(0, -r - 2), p + new Vector2(0, -35 - wave * 4), new Color(color, .65f), 1.4f, true);
                Vector2 tip = p + new Vector2(0, -39 - wave * 4);
                DrawPolyline(new[] { tip + new Vector2(0, -3), tip + new Vector2(3, 0), tip + new Vector2(0, 3), tip + new Vector2(-3, 0), tip + new Vector2(0, -3) }, color, 1, true);
            }
            if (!hover) continue;
            DrawArc(p, r + 6, 0, Mathf.Tau, 28, new Color(color, .7f), 1.3f, true);
            string title = LootName(currency) + "  +" + UiTheme.Number(pickup.N("amount"));
            float width = Math.Max(116, HudTextWidth(title, 13) + 24);
            Vector2 origin = new(Math.Clamp(p.X + 24, 8, Math.Max(8, WorldSize.X - width - 8)), Math.Clamp(p.Y - 30, 55, Math.Max(55, WorldSize.Y - 65)));
            Box(new(origin, new Vector2(width, 49)), new(.025f, .055f, .08f, .96f), new Color(color, .55f), 7);
            Text(title, origin + new Vector2(12, 19), 13, color);
            Text(pickup.List("receipts").Count > 1 ? "左键回收 · 已合并邻近掉落" : "左键回收", origin + new Vector2(12, 37), 10, UiTheme.Muted);
        }
    }
    private void DrawLootFlights()
    {
        SetUiOffset(Vector2.Zero);
        foreach (var pickup in Battle.LootPickups)
        {
            if (pickup.S("phase") != "flying") continue;
            string currency = pickup.S("currency"); Color color = LootColor(currency);
            double u = Math.Clamp(pickup.N("flight_elapsed") / pickup.N("flight_duration"), 0, 1);
            Vector2 at = LootFlightPosition(pickup, u);
            for (int i = 6; i > 0; i--)
            {
                Vector2 a = LootFlightPosition(pickup, Math.Max(0, u - i * .018)), b = LootFlightPosition(pickup, Math.Max(0, u - (i - 1) * .018));
                DrawLine(a, b, new Color(color, (7 - i) * .04f), 3, true);
            }
            float size = (float)(30 * (1 - u * .6) + 10 * Math.Sin(u * Math.PI));
            DrawCircle(at, size * .53f, new Color(color, .12f));
            if (!_lootIcons.TryGetValue(currency, out var texture))
            {
                string path = $"res://assets/ui/loot/loot_{currency}.png";
                if (ResourceLoader.Exists(path)) { texture = GD.Load<Texture2D>(path); if (texture != null) _lootIcons[currency] = texture; }
            }
            if (texture != null) DrawTextureRect(texture, new(at - Vector2.One * size * .5f, Vector2.One * size), false);
            else DrawLootGlyph(currency, at, size * .32f, color);
        }
        foreach (var (currency, pulse) in _lootPulses)
        {
            float t = (float)(pulse.Age / .6), alpha = 1 - t;
            var at = LootHudTarget(currency); var color = LootColor(currency);
            DrawArc(at, 7 + t * 20, 0, Mathf.Tau, 28, new Color(color, alpha), 2 * alpha, true);
            Text("+" + UiTheme.Number(pulse.Amount), at + new Vector2(6, 35 + t * 12), 12, new Color(color, alpha));
        }
    }
}
