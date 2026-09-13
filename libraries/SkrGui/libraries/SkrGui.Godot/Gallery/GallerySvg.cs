using System.Text;
namespace SkrGui.Gallery;
// Complete source parser: gallery_common/src/gallery_svg.cpp. Deliberately retains its temporary SVG subset.
public enum EGallerySvgDownloadState:byte {Idle,Downloading,Ready,Failed}
public sealed class GallerySvgShape
{
 public VGPath Path=new();public SRGBColor Fill=new();
 public GallerySvgShape(){}public GallerySvgShape(GallerySvgShape source){Path=new(source.Path);Fill=source.Fill;}
}
public sealed class GallerySvgDocument
{
 public Rectf ViewBox,ContentBounds;public List<GallerySvgShape> Shapes=[];
 public GallerySvgDocument(){}public GallerySvgDocument(GallerySvgDocument source){ViewBox=source.ViewBox;ContentBounds=source.ContentBounds;Shapes=source.Shapes.Select(s=>new GallerySvgShape(s)).ToList();}
 public bool IsValid()=>Shapes.Count!=0&&ContentBounds.Width()>0&&ContentBounds.Height()>0;
}
public sealed class GallerySvgSnapshot
{public EGallerySvgDownloadState State;public GallerySvgDocument Document=new();public string Error="";public ulong Generation;}
public static partial class GallerySvg
{
 private struct SvgTransform
 {
  public float A=1,B=0,C=0,D=1,E=0,F=0;
  public SvgTransform(){}
  public Offsetf Apply(Offsetf p)=>new(A*p.X+C*p.Y+E,B*p.X+D*p.Y+F);
 }
 private static SvgTransform Multiply(SvgTransform l,SvgTransform r)=>new(){A=l.A*r.A+l.C*r.B,B=l.B*r.A+l.D*r.B,C=l.A*r.C+l.C*r.D,D=l.B*r.C+l.D*r.D,E=l.A*r.E+l.C*r.F+l.E,F=l.B*r.E+l.D*r.F+l.F};
 private static void IncludeBounds(ref Rectf bounds,ref bool hasBounds,Offsetf p)
 {if(!hasBounds){bounds=new(p.X,p.Y,p.X,p.Y);hasBounds=true;return;}bounds.Left=(p.X<bounds.Left?p.X:bounds.Left);bounds.Top=(p.Y<bounds.Top?p.Y:bounds.Top);bounds.Right=(bounds.Right<p.X?p.X:bounds.Right);bounds.Bottom=(bounds.Bottom<p.Y?p.Y:bounds.Bottom);}
 private static bool Space(char c)=>c is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';
 private static bool Alpha(char c)=>(c>='A'&&c<='Z')||(c>='a'&&c<='z');
 private static char Lower(char c)=>c>='A'&&c<='Z'?(char)(c+32):c;
 private static string LowerAscii(string s)=>new(s.Select(Lower).ToArray());
 private static bool AttrBoundary(char c)=>Space(c)||c=='<'||c=='/';
 private static bool ReadXmlAttribute(string tag,string name,ref string value)
 {
  int search=0;
  while(search<tag.Length)
  {
   int namePos=tag.IndexOf(name,search,StringComparison.Ordinal);if(namePos<0)return false;
   bool left=namePos==0||AttrBoundary(tag[namePos-1]);int pos=namePos+name.Length;
   while(pos<tag.Length&&Space(tag[pos]))pos++;
   if(!left||pos>=tag.Length||tag[pos]!='='){search=namePos+name.Length;continue;}
   pos++;while(pos<tag.Length&&Space(tag[pos]))pos++;if(pos>=tag.Length)return false;
   char quote=tag[pos];if(quote!='"'&&quote!='\'')return false;
   int start=pos+1,end=tag.IndexOf(quote,start);if(end<0)return false;value=tag.Substring(start,end-start);return true;
  }return false;
 }
 private static unsafe bool ReadFloat(byte[] data,ref int cursor,ref float value)
 {
  fixed(byte* bytes=data){byte* begin=bytes+cursor;value=GalleryNative.skrgui_strtof(begin,out byte* end,out int rangeError);if(end==begin||rangeError!=0)return false;cursor=(int)(end-bytes);return true;}
 }
 private static bool ParseFloatList(string text,List<float> values)
 {
  values.Clear();byte[] data=Encoding.Latin1.GetBytes(text+'\0');int cursor=0;
  while(data[cursor]!=0){while(data[cursor]!=0&&(Space((char)data[cursor])||data[cursor]==','))cursor++;if(data[cursor]==0)break;float value=0;if(!ReadFloat(data,ref cursor,ref value))return false;values.Add(value);}
  return values.Count!=0;
 }
 private static bool ParseViewBox(string svg,ref Rectf viewBox)
 {
  int pos=svg.IndexOf("<svg",StringComparison.Ordinal);if(pos<0)return false;int end=svg.IndexOf('>',pos);if(end<0)return false;string text="";
  if(!ReadXmlAttribute(svg.Substring(pos,end-pos+1),"viewBox",ref text))return false;List<float> values=[];
  if(!ParseFloatList(text,values)||values.Count<4)return false;viewBox=Rectf.LTWH(values[0],values[1],values[2],values[3]);return values[2]>0&&values[3]>0;
 }
 private static string NearestGroupAttribute(string svg,int pathPos,string name)
 {
  int group=svg.LastIndexOf("<g",pathPos,StringComparison.Ordinal);if(group<0)return "";
  int close=svg.LastIndexOf("</g",pathPos,StringComparison.Ordinal);if(close>=0&&close>group)return "";
  int end=svg.IndexOf('>',group);if(end<0||end>pathPos)return "";string result="";ReadXmlAttribute(svg.Substring(group,end-group+1),name,ref result);return result;
 }
 private static bool HexDigit(char c,ref byte output)
 {if(c>='0'&&c<='9'){output=(byte)(c-'0');return true;}if(c>='a'&&c<='f'){output=(byte)(10+c-'a');return true;}if(c>='A'&&c<='F'){output=(byte)(10+c-'A');return true;}return false;}
 private static bool HexPair(char a,char b,ref byte output)
 {byte hi=0,lo=0;if(!HexDigit(a,ref hi)||!HexDigit(b,ref lo))return false;output=(byte)((hi<<4)|lo);return true;}
 private static bool ParseColor(string text,ref SRGBColor color)
 {
  string s=LowerAscii(text);
  if(s=="none"){color=new(0,0,0,0);return true;}if(s=="black"){color=GalleryShared.Rgba(0,0,0);return true;}if(s=="white"){color=GalleryShared.Rgba(255,255,255);return true;}if(s=="red"){color=GalleryShared.Rgba(255,0,0);return true;}
  if(s.Length==4&&s[0]=='#'){byte r=0,g=0,b=0;if(!HexDigit(s[1],ref r)||!HexDigit(s[2],ref g)||!HexDigit(s[3],ref b))return false;color=GalleryShared.Rgba((byte)(r*17),(byte)(g*17),(byte)(b*17));return true;}
  if(s.Length==7&&s[0]=='#'){byte r=0,g=0,b=0;if(!HexPair(s[1],s[2],ref r)||!HexPair(s[3],s[4],ref g)||!HexPair(s[5],s[6],ref b))return false;color=GalleryShared.Rgba(r,g,b);return true;}
  return false;
 }
 private static bool ParseTransform(string text,ref SvgTransform transform)
 {
  transform=new();int pos=0;
  while(pos<text.Length)
  {
   while(pos<text.Length&&(Space(text[pos])||text[pos]==','))pos++;if(pos>=text.Length)break;
   int start=pos;while(pos<text.Length&&Alpha(text[pos]))pos++;if(start==pos)return false;string name=LowerAscii(text.Substring(start,pos-start));while(pos<text.Length&&Space(text[pos]))pos++;if(pos>=text.Length||text[pos]!='(')return false;
   int argsStart=pos+1,argsEnd=text.IndexOf(')',argsStart);if(argsEnd<0)return false;List<float> args=[];if(!ParseFloatList(text.Substring(argsStart,argsEnd-argsStart),args))return false;
   SvgTransform next=new();
   if(name=="translate"){next.E=args[0];next.F=args.Count>1?args[1]:0;}
   else if(name=="scale"){next.A=args[0];next.D=args.Count>1?args[1]:args[0];}
   else if(name=="matrix"){if(args.Count<6)return false;next=new(){A=args[0],B=args[1],C=args[2],D=args[3],E=args[4],F=args[5]};}
   else if(name=="rotate"){float radians=args[0]*(MathF.Acos(-1)/180);float s=MathF.Sin(radians),c=MathF.Cos(radians);SvgTransform rotate=new(){A=c,B=s,C=-s,D=c};if(args.Count>=3){SvgTransform to=new(){E=-args[1],F=-args[2]},from=new(){E=args[1],F=args[2]};next=Multiply(from,Multiply(rotate,to));}else next=rotate;}
   else return false;transform=Multiply(transform,next);pos=argsEnd+1;
  }return true;
 }
 public static bool Parse(Utf8StringView svgText,ref GallerySvgDocument document,ref string error)
 {
  document=new();string svg=Encoding.Latin1.GetString(svgText.Bytes);
  if(!ParseViewBox(svg,ref document.ViewBox)){error="SVG viewBox is missing or invalid.";return false;}
  Rectf bounds=new();bool hasBounds=false;int search=0;
  while(true)
  {
   int pathPos=svg.IndexOf("<path",search,StringComparison.Ordinal);if(pathPos<0)break;int end=svg.IndexOf('>',pathPos);if(end<0){error="SVG path tag is not closed.";return false;}
   string tag=svg.Substring(pathPos,end-pathPos+1),data="";if(!ReadXmlAttribute(tag,"d",ref data)){search=end+1;continue;}
   SvgTransform transform=new();string group=NearestGroupAttribute(svg,pathPos,"transform");if(group.Length!=0&&!ParseTransform(group,ref transform)){error="SVG group transform is invalid.";return false;}
   string pathTransform="";if(ReadXmlAttribute(tag,"transform",ref pathTransform)){SvgTransform part=new();if(!ParseTransform(pathTransform,ref part)){error="SVG path transform is invalid.";return false;}transform=Multiply(transform,part);}
   GallerySvgShape shape=new();string fill="";if(!ReadXmlAttribute(tag,"fill",ref fill))fill=NearestGroupAttribute(svg,pathPos,"fill");if(fill.Length==0||!ParseColor(fill,ref shape.Fill))shape.Fill=GalleryShared.Rgba(0,0,0);
   var parser=new PathParser();if(!parser.Parse(data,transform,ref error))return false;shape.Path=parser.Path;if(!parser.HasBounds)return false;
   document.Shapes.Add(shape);IncludeBounds(ref bounds,ref hasBounds,parser.Bounds.TopLeft());IncludeBounds(ref bounds,ref hasBounds,parser.Bounds.BottomRight());search=end+1;
  }
  if(!hasBounds||document.Shapes.Count==0){error="SVG has no supported path data.";return false;}document.ContentBounds=bounds;return document.IsValid();
 }
 public static bool Parse(Utf8StringView svgText,ref GallerySvgDocument document){string ignored="";return Parse(svgText,ref document,ref ignored);}
}
