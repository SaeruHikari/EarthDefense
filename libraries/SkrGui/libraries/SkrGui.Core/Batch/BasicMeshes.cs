namespace SkrGui;
// Source: inline BasicMeshes options in mesh.hpp and BasicMeshes::text in mesh.cpp.
public static partial class BasicMeshes
{
    private static bool CollectCurveSplitLines(Rectf bounds,ColorGradient gradient,List<VGPathFlattenLine> output)
    {
        if(!gradient.IsCurve())return false;
        var curve=gradient.AsCurve()!;const float epsilon=1e-6f;
        Offsetf start=curve.Start(),end=curve.End(),axis=end-start;
        Offsetf direction=new(-axis.Y*bounds.Width(),axis.X*bounds.Height());
        foreach(var stop in curve.Stops())
        {
            float position=stop.Position;if(position<=epsilon||position>=1-epsilon)continue;
            var uv=start+axis*position;var point=new Offsetf(bounds.Left+bounds.Width()*uv.X,bounds.Top+bounds.Height()*uv.Y);
            output.Add(new(){Position=point,Direction=direction});
        }
        return output.Count!=0;
    }
    private static VGPathFlattenOptions FlattenOptions(BasicMeshOptions options)=>new(){PixelRatio=options.PixelRatio,TessellationFactor=options.TessellationFactor};
    private static VGFillOptions FillOptions(BasicMeshOptions options)=>new(){PixelRatio=options.PixelRatio,AaRadius=options.AaRadius};
    private static VGFillOptions SingleConvexFillOptions(BasicMeshOptions options)
    {var result=FillOptions(options);result.OptimizeForSingleConvex=true;return result;}
    private static VGStrokeOptions StrokeOptions(float thickness,BasicMeshOptions options)=>new(){Width=thickness,PixelRatio=options.PixelRatio,TessellationFactor=options.TessellationFactor,AaRadius=options.AaRadius};
    public static bool Text(Mesh output,ReadOnlySpan<TextRenderRect> rects,SRGBColor color,bool premultiplyColor=true)
    {
        if(rects.IsEmpty||!color.IsFinite())return false;
        output.Vertices.EnsureCapacity(output.Vertices.Count+rects.Length*4);
        output.Indices.EnsureCapacity(output.Indices.Count+rects.Length*6);
        uint packed=(premultiplyColor?color.Premultiplied():color).ToRgba32();
        foreach(var item in rects)
        {
            uint start=(uint)output.Vertices.Count;
            output.Vertices.Add(new(item.Rect.TopLeft(),item.Uv.TopLeft(),packed));
            output.Vertices.Add(new(item.Rect.TopRight(),item.Uv.TopRight(),packed));
            output.Vertices.Add(new(item.Rect.BottomRight(),item.Uv.BottomRight(),packed));
            output.Vertices.Add(new(item.Rect.BottomLeft(),item.Uv.BottomLeft(),packed));
            output.PushQuad(start,start+1,start+2,start+3);
        }
        return true;
    }
}
