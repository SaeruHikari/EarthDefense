using Godot;
namespace Earthward.Presentation;

/// <summary>One visual language for fleet arrivals and local planetary damage. Input ownership lives in Main.</summary>
public partial class TacticalAlertView : Node2D
{
    public sealed class Card
    {
        public bool Active, Preview, Acknowledged, Backside, Offscreen;
        public string Title = "", Detail = "", TargetId = "";
        public double Age, Remaining, Window, Amount;
        public Vector3 WorldPosition;
        public Vector2 Direction = Vector2.Right, Reticle;
        public Rect2 Rect;
    }
    /// <summary>How long an arrival, preview, or damage notice stays on screen before fading out.</summary>
    public const double AlertLifetime = 8;
    public Card Mother { get; } = new();
    public Card Damage { get; } = new();
    public string HoverAction { get; set; } = "";
    public string PressedAction { get; set; } = "";
    public double Clock { get; set; }
    public Rect2 PlayArea { get; set; }
    public string HitTest(Vector2 point)
    {
        if (!Visible) return "";
        foreach (var pair in new[] { (Mother, "mother"), (Damage, "damage") })
        {
            var (c, id) = pair;
            if (!c.Active || !c.Rect.HasPoint(point)) continue;
            return id + ":focus";
        }
        return "";
    }
    public static Vector2 DirectionFor(Vector3 cameraLocal)
    {
        var direction = new Vector2(cameraLocal.X, -cameraLocal.Y);
        return direction.LengthSquared() > .00001f ? direction.Normalized() : Vector2.Right;
    }
    private void Text(string text, Vector2 p, int size, Color color) => DrawString(UiTheme.Font, p, text, HorizontalAlignment.Left, -1, size, color);
    private void Line(Vector2 a, Vector2 b, Color color, float width = 1) => DrawLine(a, b, color, width, true);
    public override void _Draw()
    {
        if (Mother.Active) DrawCard(Mother, false);
        if (Damage.Active) DrawCard(Damage, true);
    }
    private void DrawCard(Card c, bool damage)
    {
        Color tone = damage ? new Color("ff756c") : new Color("f4c181");
        string id = damage ? "damage" : "mother";
        float alpha = c.Acknowledged ? .46f : 1;
        if (HoverAction.StartsWith(id)) alpha = 1;
        // Every notice fades on the same fixed schedule, including advance warnings.
        if (c.Age > AlertLifetime - 1.5) alpha *= (float)Math.Clamp((AlertLifetime - c.Age) / 1.5, 0, 1);
        float entrance = (float)Math.Clamp(c.Age / .28, 0, 1);
        alpha *= .45f + .55f * entrance;
        Color ink = UiTheme.Alpha(UiTheme.Ink, alpha), accent = UiTheme.Alpha(tone, alpha);
        var r = c.Rect; var p = r.Position;
        Vector2[] plate = [p + new Vector2(14, 0), new(r.End.X - 30, p.Y), new(r.End.X, p.Y + 30), new(r.End.X, r.End.Y - 11), new(r.End.X - 11, r.End.Y), new(p.X, r.End.Y), new(p.X, p.Y + 14)];
        DrawColoredPolygon(plate, new Color(.025f, .055f, .072f, .86f * alpha));
        DrawPolyline(plate.Append(plate[0]).ToArray(), UiTheme.Alpha(tone, .50f * alpha), 1, true);
        Line(p + new Vector2(14, 0), p + new Vector2(90, 0), accent, 2);
        Line(new(r.End.X, p.Y + 30), new(r.End.X, p.Y + 70), accent, 2);
        Text(damage ? "PLANET // IMPACT" : "TACTICAL // EARLY WARNING", p + new Vector2(16, 20), 10, UiTheme.Alpha(tone, .72f * alpha));
        // Keep the heading above the emblem so it fits even in a narrow HUD card.
        Text(UiTheme.Fit(c.Title, r.Size.X - 58, 18), p + new Vector2(16, 43), 18, ink);
        var emblem = p + new Vector2(64, 91);
        float pulse = .5f + .5f * MathF.Sin((float)Clock * 3.2f);
        DrawArc(emblem, 42, 0, Mathf.Tau, 64, UiTheme.Alpha(tone, (.10f + pulse * .08f) * alpha), 1, true);
        float sweep = (float)Clock * .5f;
        DrawArc(emblem, 46, sweep, sweep + .7f, 20, UiTheme.Alpha(tone, .7f * alpha), 1.3f, true);
        DrawArc(emblem, 46, sweep + Mathf.Pi, sweep + Mathf.Pi + .7f, 20, UiTheme.Alpha(tone, .35f * alpha), 1, true);
        // Tall hard-edged punctuation stays legible without relying on a font glyph.
        Vector2[] stem = [emblem + new Vector2(-8, -43), emblem + new Vector2(8, -43), emblem + new Vector2(5, 20), emblem + new Vector2(-5, 20)];
        DrawColoredPolygon(stem, accent);
        DrawRect(new Rect2(emblem + new Vector2(-5, 32), new Vector2(10, 10)), accent);
        Text(UiTheme.Fit(c.Detail, r.Size.X - 135, 11), p + new Vector2(122, 70), 11, UiTheme.Alpha(UiTheme.Muted, alpha));
        if (damage || c.Preview)
        {
            string value = damage ? $"−{c.Amount:0.#}" : $"{Math.Ceiling(c.Remaining):0}s";
            Text(UiTheme.Fit(value, r.Size.X - 135, 29), p + new Vector2(122, 106), 29, accent);
            Text(damage ? "8 秒内累计损伤" : "预计抵达", p + new Vector2(122, 125), 10, UiTheme.Alpha(UiTheme.Muted, alpha));
        }
        else
        {
            Text("母舰抵达", p + new Vector2(122, 103), 16, accent);
            float scanWidth = Math.Max(0, r.Size.X - 138);
            Line(p + new Vector2(122, 118), p + new Vector2(122 + scanWidth, 118), UiTheme.Alpha(tone, .24f * alpha));
            float scanX = 122 + scanWidth * ((float)Clock * .4f % 1);
            Line(p + new Vector2(scanX, 114), p + new Vector2(scanX, 122), accent, 1.4f);
        }
        Text(c.Backside ? "背面 · 点击定位" : c.Offscreen ? "视野外 · 点击定位" : "点击定位目标", p + new Vector2(16, 148), 10, UiTheme.Alpha(tone, alpha));
        // Direction is separate from the click caption and remains visible while acknowledged.
        DrawArrow(p + new Vector2(r.Size.X - 24, 45), c.Direction, accent, 8);
        if (c.Preview)
        {
            float t = (float)Math.Clamp(1 - c.Remaining / Math.Max(c.Window, .001), 0, 1);
            Line(p + new Vector2(15, r.Size.Y - 5), p + new Vector2(15 + (r.Size.X - 30) * t, r.Size.Y - 5), accent, 2);
        }
        if (HoverAction == id + ":focus" || PressedAction == id + ":focus")
            DrawPolyline(plate.Append(plate[0]).ToArray(), accent, PressedAction == id + ":focus" ? 2.4f : 1.7f, true);
        DrawTarget(c, tone, alpha);
    }
    private void DrawArrow(Vector2 p, Vector2 d, Color color, float radius)
    {
        Vector2 side = new(-d.Y, d.X);
        DrawPolyline([p - d * radius * .45f + side * radius * .55f, p + d * radius, p - d * radius * .45f - side * radius * .55f], color, 1.7f, true);
    }
    private void DrawTarget(Card c, Color tone, float alpha)
    {
        if (!PlayArea.HasPoint(c.Reticle) || Mother.Active && Mother.Rect.Grow(8).HasPoint(c.Reticle) || Damage.Active && Damage.Rect.Grow(8).HasPoint(c.Reticle)) return;
        var p = c.Reticle; var color = UiTheme.Alpha(tone, alpha * .8f);
        if (c.Backside || c.Offscreen) { DrawArrow(p, c.Direction, color, 13); return; }
        float radius = 16 + 2 * MathF.Sin((float)Clock * 2);
        foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
        {
            var corner = p + s * radius;
            Line(corner, corner - new Vector2(s.X * 7, 0), color, 1.4f);
            Line(corner, corner - new Vector2(0, s.Y * 7), color, 1.4f);
        }
        DrawCircle(p, 2, color);
    }
}
