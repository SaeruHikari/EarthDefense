using System.Runtime.InteropServices;
namespace SkrGui.Tests;
public static partial class TextRasterContractTests
{
    private delegate List<ulong> RasterSignaturesDelegate(bool lcd,ETextSubpixelPositioning positioning,ReadOnlySpan<float> positions);
    private sealed record RasterRoute(bool Advanced,bool Lcd,bool TransformsColorLayers);
    private sealed record BitmapRasterRoute(bool Advanced,bool Lcd);
    private sealed record StrikeCase(float FontSize,float NativeWidth,float NativeHeight,float SelectedWidth);
    private static void Eq(byte[] a,byte[] b)=>Check.SequenceEqual(a,b);
    private sealed class HorizontalCoverage { public float Left,Right; }
    private static float Round(float value)=>MathF.Round(value,MidpointRounding.AwayFromZero);
    private static void check_render_ranges(TextRenderResult result)
    {foreach(var cmd in result.Commands)Check.That(cmd.RectOffset+cmd.RectCount<=(ulong)result.Rects.Count);}
    private static bool horizontal_coverage(TextAtlasView atlas,TextRenderRect rect,HorizontalCoverage output)
    {
        if(atlas.Width==0||atlas.Height==0||atlas.Pixels.IsEmpty)return false;
        int xb=(int)Round(rect.Uv.Left*atlas.Width),xe=(int)Round(rect.Uv.Right*atlas.Width),yb=(int)Round(rect.Uv.Top*atlas.Height),ye=(int)Round(rect.Uv.Bottom*atlas.Height);
        if(xb<0||yb<0||xe>atlas.Width||ye>atlas.Height||xb>=xe||yb>=ye)return false;
        int first=xe,last=xb-1,channels=atlas.Format==ETextAtlasFormat.RGBA8?4:1;var pixels=atlas.Pixels.Span;
        for(int y=yb;y<ye;y++)for(int x=xb;x<xe;x++)
        {int offset=(int)(y*atlas.Stride)+x*channels;bool covered=channels==1?pixels[offset]!=0:pixels[offset]!=0||pixels[offset+1]!=0||pixels[offset+2]!=0;if(covered){first=Math.Min(first,x);last=Math.Max(last,x);}}
        if(last<first)return false;float devicePerTexel=rect.Rect.Width()/(xe-xb);output.Left=rect.Rect.Left+(first-xb)*devicePerTexel;output.Right=rect.Rect.Left+(last+1-xb)*devicePerTexel;return true;
    }
    private static bool atlas_rect_signature(TextAtlasView atlas,TextRenderRect rect,ref ulong output)
    {
        if(atlas.Width==0||atlas.Height==0||atlas.Pixels.IsEmpty)return false;
        int xb=(int)Round(rect.Uv.Left*atlas.Width),xe=(int)Round(rect.Uv.Right*atlas.Width),yb=(int)Round(rect.Uv.Top*atlas.Height),ye=(int)Round(rect.Uv.Bottom*atlas.Height);
        if(xb<0||yb<0||xe>atlas.Width||ye>atlas.Height||xb>=xe||yb>=ye)return false;
        ulong signature=1469598103934665603UL;int channels=atlas.Format==ETextAtlasFormat.RGBA8?4:1;var pixels=atlas.Pixels.Span;
        void Append(byte value){signature^=value;signature=unchecked(signature*1099511628211UL);}
        Append((byte)(xe-xb));Append((byte)(ye-yb));for(int y=yb;y<ye;y++)for(int x=xb;x<xe;x++)for(int channel=0;channel<channels;channel++)Append(pixels[(int)(y*atlas.Stride)+x*channels+channel]);output=signature;return true;
    }
    private static bool atlas_rect_rgba_pixel(TextAtlasView atlas,TextRenderRect rect,uint localX,uint localY,byte[] output)
    {
        if(atlas.Format!=ETextAtlasFormat.RGBA8||atlas.Pixels.IsEmpty)return false;
        int xb=(int)Round(rect.Uv.Left*atlas.Width),xe=(int)Round(rect.Uv.Right*atlas.Width),yb=(int)Round(rect.Uv.Top*atlas.Height),ye=(int)Round(rect.Uv.Bottom*atlas.Height),x=xb+(int)localX,y=yb+(int)localY;
        if(xb<0||yb<0||xe>atlas.Width||ye>atlas.Height||x<xb||x>=xe||y<yb||y>=ye)return false;
        for(int channel=0;channel<output.Length;channel++)output[channel]=atlas.Pixels.Span[(int)(y*atlas.Stride)+x*4+channel];return true;
    }
    private static bool atlas_has_dominant_color(TextAtlasView atlas,uint channel)
    {
        if(atlas.Format!=ETextAtlasFormat.RGBA8||channel>=3)return false;var pixels=atlas.Pixels.Span;
        for(uint y=0;y<atlas.Height;y++)for(uint x=0;x<atlas.Width;x++)
        {int p=(int)(y*atlas.Stride+x*4);uint other0=(channel+1)%3,other1=(channel+2)%3;if(pixels[p+3]!=0&&pixels[p+(int)channel]>pixels[p+(int)other0]+32&&pixels[p+(int)channel]>pixels[p+(int)other1]+32)return true;}return false;
    }
    private static TextRenderResult paint_glyph(TextLine line,Offsetf origin,float ratio=1)=>paint_glyph(line,origin,ratio,Offsetf.Zero());
    private static TextRenderResult paint_glyph(TextLine line,Offsetf origin,float ratio,Offsetf? offset)
    {TextRenderResult result=new();Check.That(line.Paint(origin,result,new(){PixelRatio=ratio,DeviceOffset=offset}));Check.Equal(1,result.Commands.Count);Check.Equal(1,result.Rects.Count);return result;}
}
