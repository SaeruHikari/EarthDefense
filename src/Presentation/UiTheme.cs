using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Earthward.Domain;
namespace Earthward.Presentation;

public static class UiTheme
{

    public static Font Font => _font ??= GD.Load<Font>("res://assets/fonts/Interface.ttf");

    public static Font Display => _display ??= GD.Load<Font>("res://assets/fonts/Display.ttf");
    private static Font? _font;
    private static Font? _display;

    public static readonly Color Ink = new("edf3f1");

    public static readonly Color Muted = new("91a6b0");

    public static readonly Color Mint = new("9be4cc");

    public static readonly Color Amber = new("eabe7e");

    public static readonly Color Coral = new("ef9e91");

    public static readonly Color Cyan = new("79c8dd");

    public static readonly Color Alien = new("b9a0ee");

    public static readonly Color Dim = new("4c636f");

    public static readonly Color Line = new("273b48");

    public static Color Alpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    public static StyleBoxFlat Panel(Color? background = null, Color? border = null, int radius = 5, int margin = 8)
    {
        var style = new StyleBoxFlat { BgColor = background ?? new Color("112631"), BorderColor = border ?? new Color("516d74"), ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = 5, ContentMarginBottom = 5 };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    public static Label Label(string text = "", int size = 12, Color? color = null, bool wrap = true)
    {
        var result = new Label { Text = text, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off, MouseFilter = Control.MouseFilterEnum.Ignore };
        result.AddThemeFontOverride("font", Font);
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", color ?? Ink);
        return result;
    }

    public static Button Button(string text = "", int size = 12)
    {
        var result = new Button { Text = text, CustomMinimumSize = new Vector2(0, 26) };
        result.AddThemeFontOverride("font", Font);
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", Ink);
        PaintButton(result);
        return result;
    }

    public static void PaintButton(Button result)
    {
        var normal = Panel(new Color("142631"), new Color("3b555e"), 4, 5);
        normal.ContentMarginTop = 3;
        normal.ContentMarginBottom = 3;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color("25403d");
        hover.BorderColor = Mint;
        result.AddThemeStyleboxOverride("normal", normal);
        result.AddThemeStyleboxOverride("hover", hover);
        result.AddThemeStyleboxOverride("pressed", hover);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color("10212a");
        disabled.BorderColor = new Color("293d47");
        result.AddThemeStyleboxOverride("disabled", disabled);
        result.AddThemeColorOverride("font_disabled_color", Muted);
        result.AddThemeStyleboxOverride("focus", Panel(new Color(0, 0, 0, 0), Mint, 4, 0));
    }

    public static void Mark(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride("normal", Panel(new Color(selected ? "264b42" : "142631"), selected ? Mint : new Color("3b555e"), 4, 5));
        button.AddThemeColorOverride("font_color", selected ? Mint : Ink);
    }

    public static string Number(double value)
    {
        if (value == 0) return "0";
        double absolute = Math.Abs(value);
        if (absolute >= 1e15)
            return value.ToString("0.##e+0", CultureInfo.InvariantCulture);
        if (absolute >= 1e12)
            return (value / 1e12).ToString("0.0", CultureInfo.InvariantCulture) + "T";
        if (absolute >= 1e9)
            return (value / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + "B";
        if (absolute >= 1e6)
            return (value / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        if (absolute >= 1e4)
            return (value / 1e3).ToString("0.0", CultureInfo.InvariantCulture) + "k";
        return Math.Floor(value).ToString("0", CultureInfo.InvariantCulture);
    }

    public static string Fit(string text, float width, int fontSize)
    {
        if (Font.GetStringSize(text, fontSize: fontSize).X <= width)
            return text;
        while (text.Length > 1 && Font.GetStringSize(text + "…", fontSize: fontSize).X > width)
            text = text[..^1];
        return text + "…";
    }

    public static string Wrap(string text, float width, int fontSize)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (char ch in paragraph)
            {
                if (line.Length > 0 && Font.GetStringSize(line + ch, fontSize: fontSize).X > width)
                {
                    lines.Add(line);
                    line = "";
                }
                line += ch;
            }
            lines.Add(line);
        }
        return string.Join('\n', lines);
    }

    public static Control Tooltip(string title, string content)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Panel());
        panel.AddThemeFontOverride("font", Font);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(238, 0) };
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);
        box.AddChild(Label(title, 13, Mint, false));
        box.AddChild(Label(Wrap(content, 238, 11), 11, Ink, false));
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        return panel;
    }

    public static IEnumerable<DataMap> Maps(IEnumerable<object?> values) => values.OfType<DataMap>();

    public static string[] Strings(IEnumerable<object?> values) => values.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? "").ToArray();

    public static void Poly(CanvasItem target, Vector2 center, float radius, Color color, float width, params Vector2[] points) => target.DrawPolyline(points.Select(p => center + p * radius).ToArray(), color, width, true);

    public static void Center(CanvasItem target, string text, Vector2 at, float width, int fontSize, Color color)
    {
        string fitted = Fit(text, Math.Max(1, width - 8), fontSize);
        float measured = Font.GetStringSize(fitted, fontSize: fontSize).X;
        target.DrawString(Font, at + new Vector2((width - measured) * .5f, 0), fitted, HorizontalAlignment.Left, -1, fontSize, color);
    }
}

