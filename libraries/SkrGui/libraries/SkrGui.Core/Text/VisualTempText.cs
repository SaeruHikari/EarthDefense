using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SkrGui;

// Source: visual/visual_temp_text.hpp and cpp @ 611561f8.
public enum EVisualTempTextAlign : byte { Start, End, Left, Right, Center }
internal static class VisualTempTextLayoutHelper
{
    internal static float FiniteMaxWidthOrInfinity(BoxConstraints c)=>c.HasBoundedWidth()?c.MaxWidth:float.PositiveInfinity;
    internal static Offsetf UnframedParagraphOffset(ETextHAlign alignment,Sizef visual,Sizef text)
    {float free=visual.Width-text.Width;return alignment==ETextHAlign.Center?new(free*.5f,0):alignment==ETextHAlign.Right?new(free,0):Offsetf.Zero();}
    internal static bool FinalizeParagraphFrame(TextParagraph paragraph,BoxConstraints constraints)
    {if(!(paragraph.MaxWidth()>0))return true;float frame=constraints.Constrain(paragraph.Size()).Width;if(!(frame>0)||MathF.Abs(frame-paragraph.MaxWidth())<=GuiConstants.FlutterPrecisionErrorTolerance)return true;paragraph.SetMaxWidth(frame);return paragraph.Shape();}
    internal static bool NearlyGreater(float lhs,float rhs)=>lhs>rhs+GuiConstants.FlutterPrecisionErrorTolerance;
    internal static SRGBColor VertexColorForCommand(TextRenderCommand command,SRGBColor color)=>command.PixelMode==ETextPixelMode.Colored?new(1,1,1,color.A):color;
    internal static Rectf CommandBound(TextRenderResult result,TextRenderCommand command)
    {
        Debug.Assert(command.RectCount>0&&command.RectOffset+command.RectCount<=(ulong)result.Rects.Count);
        Rectf bound=result.Rects[(int)command.RectOffset].Rect;ulong end=command.RectOffset+command.RectCount;
        for(ulong i=command.RectOffset+1;i<end;i++)bound=bound.Unite(result.Rects[(int)i].Rect);return bound.Intersect(command.ClipRect);
    }
    internal static bool AppendTextResult(PaintContext context,TextRenderResult result,SRGBColor color,object source)
    {
        if(result.IsEmpty())return true;
        foreach(var command in result.Commands)
        {
            if(command.RectCount==0)continue;Debug.Assert(command.RectOffset+command.RectCount<=(ulong)result.Rects.Count);
            var rects=CollectionsMarshal.AsSpan(result.Rects).Slice((int)command.RectOffset,(int)command.RectCount);var vertexColor=VertexColorForCommand(command,color);Mesh mesh=new();
            if(!BasicMeshes.Text(mesh,rects,vertexColor))continue;var bound=CommandBound(result,command);if(bound.IsEmpty())continue;
            bool clipped=command.ClipRect!=Rectf.Largest();if(clipped)context.PushClipRect(command.ClipRect);
            if(command.IsHexBoxFallback())context.DrawMesh(mesh,null,null,bound);else context.DrawText(mesh,command.AtlasIndex,command.PixelMode,bound,source);
            if(clipped)context.PopClip();
        }
        return true;
    }
}
public class VisualTempText : VisualLeaf
{
    private Utf8StringView _text;
    private TextStyle _textStyle=new();
    private SRGBColor _color=new(1,1,1,1);
    private EVisualTempTextAlign _textAlign=EVisualTempTextAlign.Start;
    private ETextDirectionMode _textDirection=ETextDirectionMode.Auto;
    private bool _softWrap=true;
    private ETextOverflow _overflow=ETextOverflow.Clip;
    private float _textScaleFactor=1;
    private ulong? _maxLines;
    private readonly List<float> _tabStops=[];
    private uint _ellipsisCodepoint=TextDefaults.EllipsisCodepoint;
    private TextParagraph? _paragraph;
    private bool _paragraphContentDirty=true,_paragraphTabStopsDirty,_paragraphLayoutValid;
    private Sizef _textSize;
    private Offsetf _paragraphOffset;
    private bool _hasVisualOverflow,_didExceedMaxLines;
    public Utf8StringView Text()=>_text;
    public ReadOnlyTextStyle TextStyle()=>new(() => _textStyle);
    public Utf8StringView FontFamilies()=>_textStyle.FontFamilies;
    public float PixelSize()=>_textStyle.FontSize;
    public SRGBColor Color()=>_color;
    public EVisualTempTextAlign TextAlign()=>_textAlign;
    public ETextDirectionMode TextDirection()=>_textDirection;
    public bool SoftWrap()=>_softWrap;
    public ETextOverflow Overflow()=>_overflow;
    public float TextScaleFactor()=>_textScaleFactor;
    public ulong? MaxLines()=>_maxLines;
    public ReadOnlySpan<float> TabStops()=>CollectionsMarshal.AsSpan(_tabStops);
    public uint EllipsisCodepoint()=>_ellipsisCodepoint;
    public Sizef TextSize()=>_textSize;
    public bool HasVisualOverflow()=>_hasVisualOverflow;
    public bool DidExceedMaxLines()=>_didExceedMaxLines;
    public void SetText(Utf8StringView text){if(_text==text)return;_text=new(text.Bytes.ToArray());MarkParagraphContentDirty();}
    public void SetTextStyle(TextStyle style){Debug.Assert(style.FontSize>0&&float.IsFinite(style.FontSize));if(IsSameTextStyle(style))return;_textStyle=style.Copy();MarkParagraphContentDirty();}
    public void SetFontFamilies(Utf8StringView families){if((Utf8StringView)_textStyle.FontFamilies==families)return;_textStyle.FontFamilies=new Utf8StringView(families.Bytes.ToArray());MarkParagraphContentDirty();}
    public void SetPixelSize(float pixelSize){Debug.Assert(pixelSize>0&&float.IsFinite(pixelSize));if(_textStyle.FontSize==pixelSize)return;_textStyle.FontSize=pixelSize;MarkParagraphContentDirty();}
    public void SetColor(SRGBColor color){Debug.Assert(color.IsFinite());if(_color==color)return;_color=color;}
    public void SetTextAlign(EVisualTempTextAlign value){if(_textAlign==value)return;_textAlign=value;InvalidateParagraphLayout();}
    public void SetTextDirection(ETextDirectionMode value){if(_textDirection==value)return;_textDirection=value;InvalidateParagraphLayout();}
    public void SetSoftWrap(bool value){if(_softWrap==value)return;_softWrap=value;InvalidateParagraphLayout();}
    public void SetOverflow(ETextOverflow value){if(_overflow==value)return;_overflow=value;InvalidateParagraphLayout();}
    public void SetTextScaleFactor(float value){Debug.Assert(value>0&&float.IsFinite(value));if(_textScaleFactor==value)return;_textScaleFactor=value;MarkParagraphContentDirty();}
    public void SetMaxLines(ulong? value){Debug.Assert(!value.HasValue||value.Value>0);if(_maxLines==value)return;_maxLines=value;InvalidateParagraphLayout();}
    public void ClearMaxLines()=>SetMaxLines(null);
    public void SetTabStops(ReadOnlySpan<float> stops)
    {bool same=_tabStops.Count==stops.Length;for(int i=0;same&&i<stops.Length;i++)same=_tabStops[i]==stops[i];if(same)return;_tabStops.Clear();foreach(float stop in stops){Debug.Assert(stop>0&&float.IsFinite(stop));_tabStops.Add(stop);}MarkParagraphContentDirty();_paragraphTabStopsDirty=true;}
    public void ClearTabStops()=>SetTabStops([]);
    public void SetEllipsisCodepoint(uint value){if(_ellipsisCodepoint==value)return;_ellipsisCodepoint=value;InvalidateParagraphLayout();}
    protected override void OnDetachOwner(VisualOwner owner)
    {(_paragraph as IDisposable)?.Dispose();_paragraph=null;_paragraphContentDirty=true;_paragraphTabStopsDirty=_tabStops.Count!=0;InvalidateParagraphLayout();}
    protected override bool CanReuseLayout()=>_paragraphLayoutValid&&_paragraph is not null&&_paragraph.IsReady();
    protected override void PerformPaint(PaintContext context)
    {
        var service=Owner()?.TextServices();var paintColor=_color.WithAlpha(_color.A*context.Opacity());if(service is null||paintColor.IsTransparent())return;
        var paragraph=_paragraph;if(!_paragraphLayoutValid||paragraph is null||!paragraph.IsReady())return;
        Rectf? clip=NeedsClipping()?Rectf.OffsetSize(Offsetf.Zero(),Size()):null;
        TextPaintDesc desc=new(){PixelRatio=context.PixelRatio(),DeviceOffset=new()};
        if(context.ResolvePixelMapping(out float ratio,out Offsetf offset)){desc.PixelRatio=ratio;desc.DeviceOffset=offset;}
        if(clip.HasValue)context.PushClipRect(clip.Value);TextRenderResult result=new();
        if(paragraph.Paint(_paragraphOffset,result,desc))VisualTempTextLayoutHelper.AppendTextResult(context,result,paintColor,paragraph);
        if(clip.HasValue)context.PopClip();
    }
    protected override void PerformLayout()
    {
        InvalidateParagraphLayout();var service=ResolveTextService();
        if(service is null||!LayoutCachedParagraph(service,Constraints()))
        {(_paragraph as IDisposable)?.Dispose();_paragraph=null;_paragraphContentDirty=true;_textSize=Sizef.Zero();_paragraphOffset=Offsetf.Zero();_hasVisualOverflow=_didExceedMaxLines=false;SetSize(Constraints().Smallest());return;}
        _textSize=_paragraph!.Size();var constrained=Constraints().Constrain(_textSize);SetSize(constrained);
        _paragraphOffset=_paragraph.MaxWidth()>0?Offsetf.Zero():VisualTempTextLayoutHelper.UnframedParagraphOffset(ResolvedHAlign(_paragraph.InferredDirection()),constrained,_textSize);
        UpdateOverflowState(_paragraph,constrained);_paragraphLayoutValid=true;
    }
    protected override float ComputeMinIntrinsicWidth(float height)
    {if(!LayoutParagraph(new(0,1,0,float.PositiveInfinity),out var paragraph))return 0;try{return paragraph!.Size().Width;}finally{(paragraph as IDisposable)?.Dispose();}}
    protected override float ComputeMaxIntrinsicWidth(float height)
    {if(!LayoutParagraph(new(0,float.PositiveInfinity,0,float.PositiveInfinity),out var paragraph,true))return 0;try{return paragraph!.NonWrappedSize().Width;}finally{(paragraph as IDisposable)?.Dispose();}}
    protected override float ComputeMinIntrinsicHeight(float width)=>ComputeTextLayoutSize(new(0,width,0,float.PositiveInfinity)).Height;
    protected override float ComputeMaxIntrinsicHeight(float width)=>ComputeMinIntrinsicHeight(width);
    protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>ComputeTextLayoutSize(constraints);
    protected override float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)
    {if(!LayoutParagraph(constraints,out var paragraph))return null;try{return BaselineFromParagraph(paragraph,baseline);}finally{(paragraph as IDisposable)?.Dispose();}}
    protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline)
    {if(!_paragraphLayoutValid||_paragraph is null||!_paragraph.IsReady())return null;float? result=BaselineFromParagraph(_paragraph,baseline);return result.HasValue?result.Value+_paragraphOffset.Y:null;}
    private TextServices? ResolveTextService()=>Owner()?.TextServices();
    private TextStyle ResolvedTextStyle(){var style=_textStyle.Copy();style.FontSize*=_textScaleFactor;return style;}
    private bool IsSameTextStyle(TextStyle style)
    {
        if(_textStyle.FontFamilies!=style.FontFamilies||_textStyle.FontSize!=style.FontSize||_textStyle.FontWeight!=style.FontWeight||_textStyle.FontStyle!=style.FontStyle||_textStyle.FontStretch!=style.FontStretch||_textStyle.FontFaces.Count!=style.FontFaces.Count||_textStyle.OpenTypeFeatures.Count!=style.OpenTypeFeatures.Count)return false;
        for(int i=0;i<style.FontFaces.Count;i++)if(_textStyle.FontFaces[i]!=style.FontFaces[i])return false;
        for(int i=0;i<style.OpenTypeFeatures.Count;i++)if(_textStyle.OpenTypeFeatures[i]!=style.OpenTypeFeatures[i])return false;return true;
    }
    private ETextHAlign ResolvedHAlign(ETextDirection direction)=>_textAlign switch
    {EVisualTempTextAlign.Left=>ETextHAlign.Left,EVisualTempTextAlign.Right=>ETextHAlign.Right,EVisualTempTextAlign.Center=>ETextHAlign.Center,EVisualTempTextAlign.Start=>direction==ETextDirection.RTL?ETextHAlign.Right:ETextHAlign.Left,EVisualTempTextAlign.End=>direction==ETextDirection.RTL?ETextHAlign.Left:ETextHAlign.Right,_=>UnreachableAlignment()};
    private static ETextHAlign UnreachableAlignment(){Debug.Fail("Unreachable source alignment");return ETextHAlign.Left;}
    private ETextOverrunBehavior ResolvedOverrunBehavior()
    {switch(_overflow){case ETextOverflow.Visible:case ETextOverflow.Clip:return ETextOverrunBehavior.NoTrimming;case ETextOverflow.Ellipsis:return ETextOverrunBehavior.TrimEllipsis;default:Debug.Fail("Unreachable source overflow");return ETextOverrunBehavior.NoTrimming;}}
    private void InvalidateParagraphLayout(){_paragraphLayoutValid=false;MarkNeedsLayout();}
    private void MarkParagraphContentDirty(){_paragraphContentDirty=true;InvalidateParagraphLayout();}
    private bool ConfigureParagraph(TextParagraph paragraph,BoxConstraints constraints,bool intrinsicWidth,bool configureContent=true,bool configureTabStops=true)
    {
        if(configureContent){paragraph.Clear();if(!paragraph.AddString(_text,ResolvedTextStyle()).IsValid())return false;}
        paragraph.SetDirection(_textDirection);float width=!intrinsicWidth&&(_softWrap||_overflow==ETextOverflow.Ellipsis)?VisualTempTextLayoutHelper.FiniteMaxWidthOrInfinity(constraints):-1;
        paragraph.SetMaxWidth(float.IsFinite(width)?width:-1);paragraph.SetTextOverrunBehavior(intrinsicWidth?ETextOverrunBehavior.NoTrimming:ResolvedOverrunBehavior());paragraph.SetEllipsisCodepoint(_ellipsisCodepoint);
        paragraph.SetMaxLinesVisible(_maxLines.HasValue?(int)Math.Min(_maxLines.Value,int.MaxValue):-1);
        if(configureTabStops)paragraph.TabAlign(TabStops());if(!paragraph.Shape())return false;
        paragraph.SetAlignment(intrinsicWidth?ETextHAlign.Left:ResolvedHAlign(paragraph.InferredDirection()));return paragraph.Shape();
    }
    private bool LayoutCachedParagraph(TextServices service,BoxConstraints constraints)
    {
        if(_paragraph is null){_paragraph=service.CreateParagraph();_paragraphContentDirty=true;_paragraphTabStopsDirty=_tabStops.Count!=0;}
        if(_paragraph is null||!ConfigureParagraph(_paragraph,constraints,false,_paragraphContentDirty,_paragraphTabStopsDirty)||!VisualTempTextLayoutHelper.FinalizeParagraphFrame(_paragraph,constraints))return false;
        _paragraphContentDirty=_paragraphTabStopsDirty=false;return true;
    }
    private bool LayoutParagraph(BoxConstraints constraints,out TextParagraph? output,bool intrinsicWidth=false)
    {
        var service=ResolveTextService();if(service is null){output=null;return false;}output=service.CreateParagraph();
        if(output is null||!ConfigureParagraph(output,constraints,intrinsicWidth,true,_tabStops.Count!=0)||!VisualTempTextLayoutHelper.FinalizeParagraphFrame(output,constraints))
        {(output as IDisposable)?.Dispose();output=null;return false;}return true;
    }
    private Sizef ComputeTextLayoutSize(BoxConstraints constraints)
    {if(!LayoutParagraph(constraints,out var paragraph))return constraints.Smallest();try{return constraints.Constrain(paragraph!.Size());}finally{(paragraph as IDisposable)?.Dispose();}}
    private static float? BaselineFromParagraph(TextParagraph? paragraph,ETextBaseline baseline)=>baseline==ETextBaseline.Alphabetic&&paragraph is not null&&paragraph.LineCount()>0?paragraph.LineAscent(0):null;
    private void UpdateOverflowState(TextParagraph paragraph,Sizef constrained)
    {var size=paragraph.Size();bool trimmed=paragraph.TrimPosition()!=ulong.MaxValue;int max=paragraph.MaxLinesVisible();bool exceeds=max>=0&&paragraph.LineCount()>(ulong)max;_hasVisualOverflow=VisualTempTextLayoutHelper.NearlyGreater(size.Width,constrained.Width)||VisualTempTextLayoutHelper.NearlyGreater(size.Height,constrained.Height)||trimmed||exceeds;_didExceedMaxLines=exceeds;}
    private bool NeedsClipping()=>_overflow!=ETextOverflow.Visible;
}
