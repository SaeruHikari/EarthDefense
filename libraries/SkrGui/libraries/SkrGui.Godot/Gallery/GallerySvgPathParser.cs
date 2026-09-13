using System.Text;
namespace SkrGui.Gallery;
public static partial class GallerySvg
{
 // Source GallerySvgPathParser: same command dispatch, relative-coordinate and smooth-control state.
 private sealed class PathParser
 {
  private byte[] _data=[0];private int _cursor,_end;private SvgTransform _transform=new();
  internal VGPath Path=new();internal Rectf Bounds;internal bool HasBounds;
  private Offsetf _current,_subPathStart,_lastCubicControl,_lastQuadControl;
  private bool _hasCurrent,_hasLastCubicControl,_hasLastQuadControl;private char _command;
  internal bool Parse(string data,SvgTransform transform,ref string error)
  {
   _data=Encoding.Latin1.GetBytes(data+'\0');_cursor=0;_end=_data.Length-1;_transform=transform;
   while(true){SkipSeparators();if(_cursor>=_end||_data[_cursor]==0)return !Path.IsEmpty();if(Alpha((char)_data[_cursor])){_command=(char)_data[_cursor];_cursor++;}else if(_command==0){error="SVG path data starts without a command.";return false;}if(!ParseCommand(ref error))return false;}
  }
  private void SkipSeparators(){while(_cursor<_end&&(Space((char)_data[_cursor])||_data[_cursor]==','))_cursor++;}
  private bool HasNumber(){SkipSeparators();if(_cursor>=_end)return false;char c=(char)_data[_cursor];return c=='-'||c=='+'||c=='.'||c>='0'&&c<='9';}
  private bool ReadNumber(ref float value){SkipSeparators();if(_cursor>=_end)return false;return ReadFloat(_data,ref _cursor,ref value);}
  private bool ReadPoint(ref Offsetf value){float x=0,y=0;if(!ReadNumber(ref x)||!ReadNumber(ref y))return false;value=new(x,y);return true;}
  private Offsetf AbsolutePoint(Offsetf point,bool relative)=>relative?new(_current.X+point.X,_current.Y+point.Y):point;
  private void WriteMoveTo(Offsetf point){_current=point;_subPathStart=point;_hasCurrent=true;Offsetf output=_transform.Apply(point);Path.MoveTo(output);IncludeBounds(ref Bounds,ref HasBounds,output);ClearSmoothControls();}
  private void WriteLineTo(Offsetf point){_current=point;Offsetf output=_transform.Apply(point);Path.LineTo(output);IncludeBounds(ref Bounds,ref HasBounds,output);ClearSmoothControls();}
  private void WriteQuadTo(Offsetf control,Offsetf point)
  {_current=point;_lastQuadControl=control;_hasLastQuadControl=true;_hasLastCubicControl=false;Offsetf outControl=_transform.Apply(control),outPoint=_transform.Apply(point);Path.QuadTo(outControl,outPoint);IncludeBounds(ref Bounds,ref HasBounds,outControl);IncludeBounds(ref Bounds,ref HasBounds,outPoint);}
  private void WriteCubicTo(Offsetf control0,Offsetf control1,Offsetf point)
  {_current=point;_lastCubicControl=control1;_hasLastCubicControl=true;_hasLastQuadControl=false;Offsetf out0=_transform.Apply(control0),out1=_transform.Apply(control1),outPoint=_transform.Apply(point);Path.CubicTo(out0,out1,outPoint);IncludeBounds(ref Bounds,ref HasBounds,out0);IncludeBounds(ref Bounds,ref HasBounds,out1);IncludeBounds(ref Bounds,ref HasBounds,outPoint);}
  private void WriteClose(){Path.Close();_current=_subPathStart;ClearSmoothControls();}
  private void ClearSmoothControls(){_hasLastCubicControl=false;_hasLastQuadControl=false;}
  private Offsetf ReflectPoint(Offsetf point)=>new(_current.X*2-point.X,_current.Y*2-point.Y);
  private bool ParseCommand(ref string error)
  {
   bool relative=_command>='a'&&_command<='z';
   switch(Lower(_command))
   {
    case 'm':return ParseMove(relative,ref error);
    case 'l':return ParseLine(relative,ref error);
    case 'h':return ParseHorizontal(relative,ref error);
    case 'v':return ParseVertical(relative,ref error);
    case 'c':return ParseCubic(relative,ref error);
    case 's':return ParseSmoothCubic(relative,ref error);
    case 'q':return ParseQuad(relative,ref error);
    case 't':return ParseSmoothQuad(relative,ref error);
    case 'z':WriteClose();return true;
    case 'a':error="SVG arc path command is not supported by the temporary parser.";return false;
    default:error="Unsupported SVG path command.";return false;
   }
  }
  private bool ParseMove(bool relative,ref string error)
  {
   bool hasPair=false,first=true;
   while(HasNumber()){Offsetf point=new();if(!ReadPoint(ref point)){error="Invalid SVG move command.";return false;}point=AbsolutePoint(point,relative);if(first){WriteMoveTo(point);first=false;}else WriteLineTo(point);hasPair=true;}
   _command=relative?'l':'L';return hasPair;
  }
  private bool ParseLine(bool relative,ref string error)
  {bool hasPair=false;while(HasNumber()){Offsetf point=new();if(!ReadPoint(ref point)){error="Invalid SVG line command.";return false;}WriteLineTo(AbsolutePoint(point,relative));hasPair=true;}return hasPair;}
  private bool ParseHorizontal(bool relative,ref string error)
  {bool hasValue=false;while(HasNumber()){float x=0;if(!ReadNumber(ref x)){error="Invalid SVG horizontal line command.";return false;}WriteLineTo(new(relative?_current.X+x:x,_current.Y));hasValue=true;}return hasValue;}
  private bool ParseVertical(bool relative,ref string error)
  {bool hasValue=false;while(HasNumber()){float y=0;if(!ReadNumber(ref y)){error="Invalid SVG vertical line command.";return false;}WriteLineTo(new(_current.X,relative?_current.Y+y:y));hasValue=true;}return hasValue;}
  private bool ParseCubic(bool relative,ref string error)
  {bool hasCurve=false;while(HasNumber()){Offsetf c0=new(),c1=new(),point=new();if(!ReadPoint(ref c0)||!ReadPoint(ref c1)||!ReadPoint(ref point)){error="Invalid SVG cubic command.";return false;}WriteCubicTo(AbsolutePoint(c0,relative),AbsolutePoint(c1,relative),AbsolutePoint(point,relative));hasCurve=true;}return hasCurve;}
  private bool ParseSmoothCubic(bool relative,ref string error)
  {bool hasCurve=false;while(HasNumber()){Offsetf c1=new(),point=new();if(!ReadPoint(ref c1)||!ReadPoint(ref point)){error="Invalid SVG smooth cubic command.";return false;}Offsetf c0=_hasLastCubicControl?ReflectPoint(_lastCubicControl):_current;WriteCubicTo(c0,AbsolutePoint(c1,relative),AbsolutePoint(point,relative));hasCurve=true;}return hasCurve;}
  private bool ParseQuad(bool relative,ref string error)
  {bool hasCurve=false;while(HasNumber()){Offsetf control=new(),point=new();if(!ReadPoint(ref control)||!ReadPoint(ref point)){error="Invalid SVG quadratic command.";return false;}WriteQuadTo(AbsolutePoint(control,relative),AbsolutePoint(point,relative));hasCurve=true;}return hasCurve;}
  private bool ParseSmoothQuad(bool relative,ref string error)
  {bool hasCurve=false;while(HasNumber()){Offsetf point=new();if(!ReadPoint(ref point)){error="Invalid SVG smooth quadratic command.";return false;}Offsetf control=_hasLastQuadControl?ReflectPoint(_lastQuadControl):_current;WriteQuadTo(control,AbsolutePoint(point,relative));hasCurve=true;}return hasCurve;}
 }
}
