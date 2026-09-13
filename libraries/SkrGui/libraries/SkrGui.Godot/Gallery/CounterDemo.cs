using SkrGui.Gallery;
namespace SkrGui.Demo;

// Source: samples/demo/src/gui_demo_main.cpp. The sample's Widget/State/input logic is preserved.
public enum ECounterAction:byte{None,Decrement,Reset,Increment}
public enum ECounterKey:byte{Other,Left,Down,Minus,Subtract,R,Right,Up,Plus,Add}
public sealed class CounterDemo:IDisposable
{
    public const int DemoWidth=960,DemoHeight=720;
    public const float DemoWindowScale=.92f;
    private BuildOwner? _buildOwner;private Nexus? _root;private CounterDemoState? _state;
    private VisualBackend? _backend;private GalleryTextServices _textServices=new();private VisualOwner? _visualOwner;
    private EGalleryTextAAMode _textAaMode=EGalleryTextAAMode.Sdf;
    private readonly List<VisualCounterButton> _buttons=[];private VisualCounterButton? _hoveredButton,_pressedButton;
    internal static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public bool Initialize(VisualBackend? backend,float pixelRatio,EGalleryTextAAMode mode)
    {
        Shutdown();if(backend==null)return false;_backend=backend;_textAaMode=mode;
        if(!GalleryShared.PrepareTextServices(_textServices,mode,pixelRatio))return false;
        _visualOwner=new(_textServices.Services!,backend);_buildOwner=new();
        var widget=new CounterDemoWidget(){Demo=this};_root=widget.CreateNexus();_buildOwner.AddRoot(_root);_buildOwner.BuildScope(_root);_buildOwner.FinalizeNexus();SyncVisualRoot();
        return _state!=null&&_visualOwner.Root()!=null;
    }
    public void Shutdown()
    {
        _visualOwner?.ClearRoot();
        if(_root!=null){Require(_buildOwner!=null,"CounterDemo root requires BuildOwner");_buildOwner!.RemoveRoot(_root);_buildOwner.FinalizeNexus();_root=null;}
        _visualOwner?.Dispose();_visualOwner=null;_backend=null;_buildOwner?.Dispose();_buildOwner=null;
        _textServices.Services?.Dispose();_textServices=new();_state=null;_buttons.Clear();_hoveredButton=_pressedButton=null;
    }
    public void Dispose()=>Shutdown();
    public bool Render(RenderTarget? target)
    {
        if(_buildOwner==null||_root==null||_visualOwner==null||_backend==null||_textServices.Services==null||target==null||target.VisualBackend()!=_backend)return false;
        _buildOwner.BuildScope(_root);_buildOwner.FinalizeNexus();SyncVisualRoot();
        var pixelSize=target.PixelSize();float x=(float)pixelSize.Width/DemoWidth,y=(float)pixelSize.Height/DemoHeight,ratio=MathF.Max(x,y);
        bool uniform=(long)pixelSize.Width*DemoHeight==(long)pixelSize.Height*DemoWidth;
        if(!GalleryShared.PrepareTextServices(_textServices,_textAaMode,ratio))return false;
        _visualOwner.Layout(BoxConstraints.Tight(new(DemoWidth,DemoHeight)));
        if(!_visualOwner.Paint(new(){PixelRatio=ratio,AaRadius=GalleryShared.GeometryAaRadius,ScaleOffsetOnly=uniform}))return false;
        return _visualOwner.Render(new(){Target=target,Clear=true,ClearColor=GalleryShared.Rgba(8,12,24)});
    }
    public void PointerMove(Offsetf position)=>SetHoveredButton(HitTestButton(position));
    public void PointerLeave()
    {
        SetHoveredButton(null);if(_pressedButton!=null){var pressed=_pressedButton;_pressedButton=null;pressed.State!.Release(false);}
    }
    public void PointerDown(Offsetf position)
    {PointerMove(position);_pressedButton?.State!.Release(false);_pressedButton=_hoveredButton;_pressedButton?.State!.SetPressed(true);}
    public void PointerUp(Offsetf position)
    {PointerMove(position);if(_pressedButton!=null){var pressed=_pressedButton;_pressedButton=null;pressed.State!.Release(pressed==_hoveredButton);}}
    public void KeyDown(ECounterKey key)
    {
        if(_state==null)return;
        switch(key){case ECounterKey.Left:case ECounterKey.Down:case ECounterKey.Minus:case ECounterKey.Subtract:_state.Apply(ECounterAction.Decrement);break;
            case ECounterKey.R:_state.Apply(ECounterAction.Reset);break;
            case ECounterKey.Right:case ECounterKey.Up:case ECounterKey.Plus:case ECounterKey.Add:_state.Apply(ECounterAction.Increment);break;}
    }
    internal void AttachState(CounterDemoState state){Require(state!=null&&_state==null,"CounterDemo requires its single State");_state=state;}
    internal void DetachState(CounterDemoState state){Require(_state==state,"CounterDemo can detach only its State");_state=null;}
    internal void RegisterButton(VisualCounterButton visual){Require(visual!=null&&visual.State!=null,"CounterDemo button requires State");if(!_buttons.Contains(visual))_buttons.Add(visual);}
    internal void UnregisterButton(VisualCounterButton visual)
    {Require(visual!=null,"CounterDemo requires a valid button Visual");if(_hoveredButton==visual)_hoveredButton=null;if(_pressedButton==visual)_pressedButton=null;_buttons.Remove(visual);}
    private VisualCounterButton? HitTestButton(Offsetf position)
    {
        var result=new VisualHitTestResult();if(!_visualOwner!.HitTest(result,position))return null;
        foreach(var entry in result.Path())foreach(var button in _buttons)if(entry.Target==button)return button;return null;
    }
    private void SetHoveredButton(VisualCounterButton? visual)
    {
        if(_hoveredButton==visual)return;_hoveredButton?.State!.SetHovered(false);_hoveredButton=visual;_hoveredButton?.State!.SetHovered(true);
    }
    private void SyncVisualRoot(){var visual=_root?.Visual();Require(visual!=null,"CounterDemo requires a root Visual");if(_visualOwner!.Root()!=visual)_visualOwner.SetRoot(visual);}
}
internal sealed class CounterDemoState:State
{
    internal CounterDemo? Demo;internal int Count;
    internal CounterDemoState(CounterDemo demo){CounterDemo.Require(demo!=null,"CounterDemoState requires CounterDemo");Demo=demo;demo.AttachState(this);}
    public override void Dispose(){if(Demo!=null){Demo.DetachState(this);Demo=null;}base.Dispose();}
    internal void Apply(ECounterAction action)
    {
        switch(action){case ECounterAction.Decrement:Count--;break;case ECounterAction.Reset:Count=0;break;case ECounterAction.Increment:Count++;break;default:return;}
        MarkNeedsRebuild();
    }
}
internal sealed class CounterButtonState:State
{
    internal bool IsHovered,IsPressed;
    internal void SetHovered(bool value){if(IsHovered==value)return;IsHovered=value;MarkNeedsRebuild();}
    internal void SetPressed(bool value){if(IsPressed==value)return;IsPressed=value;MarkNeedsRebuild();}
    internal void Release(bool activate)
    {
        bool wasPressed=IsPressed;IsPressed=false;if(wasPressed)MarkNeedsRebuild();if(!wasPressed||!activate)return;
        ((CounterButtonWidget)Context().Widget()).OnPressed?.Invoke();
    }
}
internal sealed class VisualCounterButton:VisualProxy
{
    internal CounterButtonState? State;
    protected override bool HitTestSelf(Offsetf position)=>true;
}
internal sealed class CounterButtonTargetWidget:VisualWidgetSingleChild
{
    internal CounterDemo? Demo;internal CounterButtonState? State;
    protected override VisualNode CreateVisual(BuildContext context)
    {CounterDemo.Require(Demo!=null&&State!=null,"CounterButtonTarget requires Demo and State");var result=new VisualCounterButton(){State=State};Demo!.RegisterButton(result);return result;}
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {CounterDemo.Require(Demo!=null&&State!=null,"CounterButtonTarget requires Demo and State");var result=(VisualCounterButton)visual;result.State=State;Demo!.RegisterButton(result);}
    protected override void DidUnmountVisual(VisualNode visual){var button=(VisualCounterButton)visual;Demo?.UnregisterButton(button);button.State=null;}
}
internal static class CounterWidgets
{
    internal static SRGBColor Color(byte r,byte g,byte b,byte a=255)=>GalleryShared.Rgba(r,g,b,a);
    internal static TempText MakeText(string text,float size,SRGBColor color,EFontWeight weight=EFontWeight.Regular,EVisualTempTextAlign align=EVisualTempTextAlign.Start)=>
        new(){Text=text,TextStyle=new(){FontFamilies=GalleryShared.DefaultFontFamilies(),FontSize=size,FontWeight=weight},Color=color,TextAlign=align,Overflow=ETextOverflow.Ellipsis,MaxLines=1};
    internal static Border MakeRoundedPanel(Widget? child,SRGBColor background,SRGBColor border,float radius,float width=0)=>
        new(){Radius=VisualBorderRadius.All(Radius.Circular(radius)),BorderThickness=EdgeInsets.All(width),BorderColor=border,BackgroundColor=background,Child=child};
}
internal sealed class CounterButtonWidget:ComponentWidget
{
    internal CounterDemo? Demo;internal string Label="";internal float LabelSize=13;internal bool IsPrimary;internal Action? OnPressed;
    protected override State CreateState()=>new CounterButtonState();
    protected override Widget Build(BuildContext context,State? state)
    {
        var button=(CounterButtonWidget)context.Widget();var value=(CounterButtonState)state!;CounterDemo.Require(button.Demo!=null&&value!=null,"CounterButton requires Demo and State");
        var background=button.IsPrimary?CounterWidgets.Color(116,92,255):CounterWidgets.Color(30,41,68);
        var border=button.IsPrimary?CounterWidgets.Color(151,134,255):CounterWidgets.Color(55,70,105);var foreground=CounterWidgets.Color(244,247,255);
        if(value!.IsHovered){background=button.IsPrimary?CounterWidgets.Color(132,109,255):CounterWidgets.Color(39,53,86);border=button.IsPrimary?CounterWidgets.Color(180,169,255):CounterWidgets.Color(78,96,137);}
        if(value.IsPressed&&value.IsHovered){background=button.IsPrimary?CounterWidgets.Color(91,66,221):CounterWidgets.Color(21,29,49);border=button.IsPrimary?CounterWidgets.Color(132,113,239):CounterWidgets.Color(59,75,111);foreground=CounterWidgets.Color(219,224,241);}
        return new SizedBox(){Height=60,Child=new CounterButtonTargetWidget(){Demo=button.Demo,State=value,Child=CounterWidgets.MakeRoundedPanel(
            new Column(){MainAxisAlignment=EMainAxisAlignment.Center,CrossAxisAlignment=ECrossAxisAlignment.Center,Children=[CounterWidgets.MakeText(button.Label,button.LabelSize,foreground,EFontWeight.SemiBold)]},background,border,14,1)}};
    }
}
internal sealed class CounterDemoWidget:ComponentWidget
{
    internal CounterDemo? Demo;
    protected override State CreateState(){CounterDemo.Require(Demo!=null,"CounterDemoWidget requires Demo");return new CounterDemoState(Demo!);}
    protected override Widget Build(BuildContext context,State? state)
    {
        var value=(CounterDemoState)state!;CounterDemo.Require(value!=null,"CounterDemoWidget requires CounterDemoState");
        Widget brand=new SizedBox(){Width=46,Height=46,Child=CounterWidgets.MakeRoundedPanel(
            new Column(){MainAxisAlignment=EMainAxisAlignment.Center,CrossAxisAlignment=ECrossAxisAlignment.Center,Children=[CounterWidgets.MakeText("S",20,CounterWidgets.Color(248,249,255),EFontWeight.Bold)]},
            CounterWidgets.Color(116,92,255),CounterWidgets.Color(172,158,255),13,1)};
        var titles=new Column(){MainAxisSize=EMainAxisSize.Min,CrossAxisAlignment=ECrossAxisAlignment.Start,Spacing=3,Children=[
            CounterWidgets.MakeText("SkrGui Counter",18,CounterWidgets.Color(242,245,255),EFontWeight.SemiBold),
            CounterWidgets.MakeText("Persistent state · declarative widgets",12,CounterWidgets.Color(139,153,184))]};
        Widget badge=CounterWidgets.MakeRoundedPanel(new Padding(){PaddingValue=EdgeInsets.Symmetric(11,6),Child=CounterWidgets.MakeText("LIVE",10,CounterWidgets.Color(117,231,187),EFontWeight.SemiBold,EVisualTempTextAlign.Center)},
            CounterWidgets.Color(24,61,57),CounterWidgets.Color(42,99,86),999,1);
        var header=new Row(){Spacing=14,CrossAxisAlignment=ECrossAxisAlignment.Center,Children=[brand,new Expanded(){Child=titles},badge]};
        Expanded MakeButton(string label,float size,bool primary,ECounterAction action)=>new(){Child=new CounterButtonWidget(){Demo=Demo,Label=label,LabelSize=size,IsPrimary=primary,OnPressed=()=>value!.Apply(action)}};
        var actions=new Row(){Spacing=12,CrossAxisAlignment=ECrossAxisAlignment.Center,Children=[MakeButton("-",25,false,ECounterAction.Decrement),MakeButton("RESET",13,false,ECounterAction.Reset),MakeButton("+",25,true,ECounterAction.Increment)]};
        var content=new Column(){Spacing=18,CrossAxisAlignment=ECrossAxisAlignment.Stretch,Children=[
            header,
            CounterWidgets.MakeRoundedPanel(new Padding(){PaddingValue=EdgeInsets.Symmetric(14,8),Child=CounterWidgets.MakeText("COMPONENT STATE",10,CounterWidgets.Color(159,146,255),EFontWeight.SemiBold,EVisualTempTextAlign.Center)},
                CounterWidgets.Color(37,32,70),CounterWidgets.Color(66,54,113),999,1),
            new Expanded(){Child=new Center(){Child=new Column(){MainAxisSize=EMainAxisSize.Min,CrossAxisAlignment=ECrossAxisAlignment.Center,Spacing=8,Children=[
                CounterWidgets.MakeText(value!.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),92,CounterWidgets.Color(248,249,255),EFontWeight.DemiBold,EVisualTempTextAlign.Center),
                CounterWidgets.MakeText("Every action rebuilds only the dirty component",12.5f,CounterWidgets.Color(133,148,180),EFontWeight.Regular,EVisualTempTextAlign.Center)]}}},
            actions,
            CounterWidgets.MakeText("Keyboard  - / +  ·  Arrow keys  ·  R to reset",11,CounterWidgets.Color(99,114,145),EFontWeight.Regular,EVisualTempTextAlign.Center)]};
        Widget card=new SizedBox(){Width=552,Height=574,Child=new Stack(){Fit=EStackFit.Expand,ClipBehavior=EClipBehavior.None,Children=[
            new Align(){Alignment=Alignment.BottomRight(),Child=new SizedBox(){Width=540,Height=560,Child=CounterWidgets.MakeRoundedPanel(null,CounterWidgets.Color(3,7,17,160),CounterWidgets.Color(3,7,17,0),32)}},
            new Align(){Alignment=Alignment.TopLeft(),Child=new SizedBox(){Width=540,Height=560,Child=CounterWidgets.MakeRoundedPanel(new Padding(){PaddingValue=EdgeInsets.All(38),Child=content},
                CounterWidgets.Color(19,27,48),CounterWidgets.Color(48,62,94),28,1)}}]}};
        var background=new Stack(){Fit=EStackFit.Expand,ClipBehavior=EClipBehavior.HardEdge,Children=[
            new Positioned(){Top=-128,Right=-102,Width=340,Height=340,Child=CounterWidgets.MakeRoundedPanel(null,CounterWidgets.Color(111,76,255,31),CounterWidgets.Color(153,127,255,24),170,1)},
            new Positioned(){Left=-110,Bottom=-145,Width=360,Height=360,Child=CounterWidgets.MakeRoundedPanel(null,CounterWidgets.Color(33,206,176,20),CounterWidgets.Color(61,215,191,18),180,1)},
            new Center(){Child=card}]};
        return CounterWidgets.MakeRoundedPanel(background,CounterWidgets.Color(8,13,27),CounterWidgets.Color(8,13,27),0);
    }
}
