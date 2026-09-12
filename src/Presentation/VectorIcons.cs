using Godot;
using System;
namespace Earthward.Presentation;

public static class VectorIcons
{

    public static void Draw(CanvasItem target, string icon, Vector2 p, float r, Color color, float width = 1.5f)
    {
        void Line(Vector2 a, Vector2 b) => target.DrawLine(p + a * r, p + b * r, color, width, true);
        void Poly(params Vector2[] pts) => UiTheme.Poly(target, p, r, color, width, pts);
        switch (icon)
        {
            case "damage":
            case "targeting":
            case "kinetic":
                Line(new(-.72f, .76f), new(.64f, -.7f));
                Poly(new(-.1f, -.75f), new(.68f, -.75f), new(.68f, .03f));
                Line(new(-.85f, -.12f), new(.12f, .85f));
                break;
            case "cycle":
            case "assembly":
            case "ricochet":
                target.DrawArc(p, r * .75f, -Mathf.Pi * .45f, Mathf.Pi * 1.1f, 16, color, width, true);
                Poly(new(-.95f, .12f), new(-.73f, -.34f), new(-.3f, -.1f));
                if (icon == "assembly")
                    Line(new(0, -.36f), new(0, .32f));
                break;
            case "speed":
            case "navigation":
                foreach (float x in new[] { -.38f, .42f })
                    Poly(new(x - .3f, -.7f), new(x + .25f, 0), new(x - .3f, .7f));
                break;
            case "range":
            case "pierce":
                Line(new(-1, 0), new(1, 0));
                foreach (float side in new[] { -1f, 1f })
                    Poly(new(side * .5f, -.45f), new(side * .98f, 0), new(side * .5f, .45f));
                break;
            case "armor":
            case "shield":
            case "crack":
                Poly(new(-.7f, -.65f), new(.7f, -.65f), new(.58f, .3f), new(0, .92f), new(-.58f, .3f), new(-.7f, -.65f));
                if (icon == "crack")
                    Poly(new(.1f, -.62f), new(-.22f, -.05f), new(.24f, .2f), new(-.1f, .7f));
                else if (icon == "shield")
                    target.DrawArc(p - new Vector2(0, r * .05f), r * .31f, 0, Mathf.Tau, 10, color, width * .7f, true);
                break;
            case "blast":
            case "orbit":
            case "slow":
                target.DrawArc(p, r * .66f, 0, Mathf.Tau, 16, color, width, true);
                if (icon == "blast")
                    foreach (var v in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                        Line(v * .57f, v);
                else if (icon == "slow")
                {
                    Line(Vector2.Zero, new(0, -.4f));
                    Line(Vector2.Zero, new(.35f, .2f));
                }
                else
                {
                    target.DrawCircle(p, r * .15f, color);
                    target.DrawCircle(p + new Vector2(.58f, -.31f) * r, r * .25f, color);
                }
                break;
            case "turn":
            case "return":
                Poly(new(-.7f, .67f), new(-.7f, -.12f), new(-.18f, -.68f), new(.76f, -.68f));
                Poly(new(.26f, -.98f), new(.8f, -.68f), new(.26f, -.28f));
                if (icon == "return")
                    Line(new(-.9f, .84f), new(.4f, .84f));
                break;
            case "chip":
                Poly(new(-.56f, -.66f), new(.36f, -.66f), new(.66f, -.36f), new(.66f, .66f), new(-.66f, .66f), new(-.66f, -.56f), new(-.56f, -.66f));
                Poly(new(0, -.32f), new(.3f, 0), new(0, .32f), new(-.3f, 0), new(0, -.32f));
                foreach (float pin in new[] { -.36f, 0f, .36f })
                {
                    Line(new(-.66f, pin), new(-.96f, pin));
                    Line(new(.66f, pin), new(.96f, pin));
                    Line(new(pin, -.66f), new(pin, -.96f));
                    Line(new(pin, .66f), new(pin, .96f));
                }
                break;
            case "mineral":
            case "core":
            case "energy_break":
            case "laser":
                Poly(new(0, -.97f), new(.72f, 0), new(0, .97f), new(-.72f, 0), new(0, -.97f));
                if (icon == "energy_break" || icon == "laser")
                    Line(new(-.9f, .45f), new(.9f, -.45f));
                else
                    Line(new(0, -.7f), new(0, .7f));
                break;
            case "power":
            case "energy":
                Poly(new(.28f, -1), new(-.5f, .12f), new(.42f, .12f), new(-.27f, 1));
                break;
            case "science":
            case "cluster":
                foreach (float a in new[] { -Mathf.Pi * .5f, Mathf.Pi * .16f, Mathf.Pi * .84f })
                {
                    var at = p + Vector2.FromAngle(a) * r * .58f;
                    target.DrawLine(p, at, color, width, true);
                    target.DrawArc(at, r * .3f, 0, Mathf.Tau, 10, color, width * .8f, true);
                }
                break;
            case "capacity":
            case "hangar":
            case "expanded_hangar":
                target.DrawRect(new Rect2(p - new Vector2(.8f, .66f) * r, new Vector2(1.6f, 1.35f) * r), color, false, width);
                Line(new(0, -.6f), new(0, .62f));
                break;
            case "repair":
                Line(new(-.8f, 0), new(.8f, 0));
                Line(new(0, -.8f), new(0, .8f));
                break;
            case "death_blast":
            case "last_stand":
                for (int i = 0; i < 6; i++)
                {
                    var v = Vector2.FromAngle(Mathf.Tau * i / 6f);
                    Line(v * .28f, v * .95f);
                }
                break;
            case "salvo":
                foreach (float x in new[] { -.48f, .48f })
                {
                    Poly(new(x, -.94f), new(x + .23f, -.37f), new(x + .23f, .62f), new(x - .23f, .62f), new(x - .23f, -.37f), new(x, -.94f));
                    Line(new(x, .67f), new(x, .95f));
                }
                break;
            case "missile":
                Poly(new(0, -1), new(.35f, -.25f), new(.35f, .75f), new(-.35f, .75f), new(-.35f, -.25f), new(0, -1));
                Line(new(-.7f, .85f), new(-.35f, .25f));
                Line(new(.7f, .85f), new(.35f, .25f));
                break;
            case "interceptor":
                Poly(new(0, -1), new(.72f, .75f), new(0, .42f), new(-.72f, .75f), new(0, -1));
                Line(new(0, .5f), new(0, 1.1f));
                break;
            case "refraction":
                Line(new(-1, 0), Vector2.Zero);
                foreach (float y in new[] { -.65f, 0, .65f })
                    Line(Vector2.Zero, new(1, y));
                break;
            case "erosion":
                target.DrawArc(p, r * .8f, .1f * Mathf.Pi, 1.9f * Mathf.Pi, 20, color, width, true);
                foreach (float x in new[] { -.4f, 0, .4f })
                    Line(new(x - .15f, .45f), new(x + .15f, -.45f));
                break;
            default:
                target.DrawArc(p, r * .6f, 0, Mathf.Tau, 16, color, width, true);
                foreach (var v in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                    Line(v * .45f, v);
                break;
        }
    }
}
