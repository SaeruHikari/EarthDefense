using System.Diagnostics;
namespace SkrGui;
public sealed partial class VGPath
{
    // Source: src/vg/vg_path.cpp:626-738.
    public void FlattenTo(VGPathFlatten output, in VGPathFlattenOptions options)
    {
        float quadTolerance = QuadBezier.CalcTolerance(options.TessellationFactor, options.PixelRatio);
        float cubicTolerance = CubicBezier.CalcTolerance(options.TessellationFactor, options.PixelRatio);
        output.Clear(); bool hasCurrentPosition = false;
        Offsetf currentPosition = default, contourStartPosition = default;
        foreach (var command in _commands)
        {
            switch (command.Type)
            {
                case EVGPathCommandType.MoveTo:
                    output.NextContour(); output.AddNode(command.Move.Target, EVGPathFlattenNodeFlags.Corner);
                    currentPosition = contourStartPosition = command.Move.Target; hasCurrentPosition = true; break;
                case EVGPathCommandType.LineTo:
                    output.AddNode(command.Line.Target, EVGPathFlattenNodeFlags.Corner); currentPosition = command.Line.Target; break;
                case EVGPathCommandType.QuadTo:
                    if (!hasCurrentPosition) { Debug.Assert(false, "QuadTo requires a current position"); break; }
                    var quad = QuadBezier.QuadTo(currentPosition, command.Quad.Control, command.Quad.Target);
                    quad.Sample(new ShapeToleranceSampleDesc { Tolerance = quadTolerance, Direction = EShapeSampleDirection.Forward, MaxDepth = options.MaxDepth },
                        (point, t) => output.AddNode(point, t >= 1 ? EVGPathFlattenNodeFlags.Corner : EVGPathFlattenNodeFlags.None));
                    currentPosition = command.Quad.Target; break;
                case EVGPathCommandType.CubicTo:
                    if (!hasCurrentPosition) { Debug.Assert(false, "CubicTo requires a current position"); break; }
                    var cubic = CubicBezier.CubicTo(currentPosition, command.Cubic.Control0, command.Cubic.Control1, command.Cubic.Target);
                    cubic.Sample(new ShapeToleranceSampleDesc { Tolerance = cubicTolerance, Direction = EShapeSampleDirection.Forward, MaxDepth = options.MaxDepth },
                        (point, t) => output.AddNode(point, t >= 1 ? EVGPathFlattenNodeFlags.Corner : EVGPathFlattenNodeFlags.None));
                    currentPosition = command.Cubic.Target; break;
                case EVGPathCommandType.Close:
                    output.CloseContour(command.Closing.WindingHint); currentPosition = contourStartPosition; hasCurrentPosition = false; break;
            }
        }
        output.Finalize();
    }
    public VGPathFlatten Flatten(in VGPathFlattenOptions options) { var result = new VGPathFlatten(); FlattenTo(result, options); return result; }
}
