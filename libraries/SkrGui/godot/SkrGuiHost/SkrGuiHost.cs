using Godot;
using SkrGui;
using SkrGui.Demo;
using SkrGui.Gallery;
using SkrGui.Godot;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

// Godot replaces only the window/input/render host of samples/demo/gui_demo_main.cpp.
public partial class SkrGuiHost:Node2D
{
    private GodotVisualBackend? _backend;
    private GodotRenderTarget? _target;
    private CounterDemo? _demo;
    private Sprite2D? _screen;
    private bool _ready,_verifying,_inVerification,_failed;
    private int _frames,_maxFrames;
    private string _output="";
    public override void _Ready()
    {
        try
        {
            GetWindow().Unresizable=true; // gallery_window_create sets is_resizable=false.
            var args=OS.GetCmdlineUserArgs();
            _verifying=args.Contains("--self-test");
            string? frames=args.FirstOrDefault(x=>x.StartsWith("--frames="));
            if(frames!=null)int.TryParse(frames[9..],out _maxFrames);
            _output=args.FirstOrDefault(x=>x.StartsWith("--output="))?[9..]??ProjectSettings.GlobalizePath("res://../../artifacts/godot");
            var aa=EGalleryTextAAMode.Sdf;
            string? mode=args.FirstOrDefault(x=>x.StartsWith("--text-aa="));
            if(mode!=null&&!GalleryShared.ParseTextAaMode(mode[10..],ref aa))throw new ArgumentException("Unknown --text-aa mode.");
            _backend=new GodotVisualBackend();
            var size=GetViewportRect().Size;_target=_backend.CreateTarget(new((int)size.X,(int)size.Y));
            _screen=new Sprite2D(){Centered=false,Texture=_target.DisplayTexture};
            var shader=new global::Godot.Shader(){Code="shader_type canvas_item; render_mode unshaded, blend_disabled; void fragment(){COLOR=texture(TEXTURE,UV);}"};
            _screen.Material=new ShaderMaterial(){Shader=shader};AddChild(_screen);
            _demo=new CounterDemo();
            if(!_demo.Initialize(_backend,MathF.Max(size.X/CounterDemo.DemoWidth,size.Y/CounterDemo.DemoHeight),aa))throw new InvalidOperationException("The original text service and Nexus tree failed to initialize.");
            GetViewport().SizeChanged+=Resize;
            GetWindow().MouseExited+=()=>_demo?.PointerLeave();
            _ready=true;GD.Print("SkrGui C# / Godot host ready.");
        }
        catch(Exception error){Fail(error);}
    }
    private void Resize()
    {if(_target==null)return;var size=GetViewportRect().Size;_target.Resize(new(Math.Max(1,(int)size.X),Math.Max(1,(int)size.Y)));}
    public override void _Process(double delta)
    {
        if(!_ready||_failed)return;
        try
        {
            if(_backend!.LastRenderError!=null)throw new InvalidOperationException("SkrGui GPU submission failed.",_backend.LastRenderError);
            if(!_demo!.Render(_target))throw new InvalidOperationException("SkrGui failed to submit the original CounterDemo frame.");
            _frames++;
            if(_verifying&&_frames>=20&&!_inVerification){_inVerification=true;_ = Verify();}
            else if(!_verifying&&_maxFrames>0&&_frames>=_maxFrames)GetTree().Quit();
        }
        catch(Exception error){Fail(error);}
    }
    private Offsetf LogicPosition(Vector2 position)
    {
        var size=GetViewportRect().Size;
        return new(position.X*CounterDemo.DemoWidth/Math.Max(1,size.X),position.Y*CounterDemo.DemoHeight/Math.Max(1,size.Y));
    }
    public override void _Input(InputEvent input)
    {
        if(!_ready||_failed)return;
        try
        {
            switch(input)
            {
                case InputEventMouseMotion motion:_demo!.PointerMove(LogicPosition(motion.Position));break;
                case InputEventMouseButton button when button.ButtonIndex==MouseButton.Left:
                    if(button.Pressed)_demo!.PointerDown(LogicPosition(button.Position));else _demo!.PointerUp(LogicPosition(button.Position));break;
                case InputEventKey key when key.Pressed&&!key.Echo:
                    if(key.Keycode==global::Godot.Key.Escape){GetTree().Quit();break;}
                    _demo!.KeyDown(key.Keycode switch{
                        global::Godot.Key.Left=>ECounterKey.Left,global::Godot.Key.Down=>ECounterKey.Down,global::Godot.Key.Minus=>ECounterKey.Minus,
                        global::Godot.Key.KpSubtract=>ECounterKey.Subtract,global::Godot.Key.R=>ECounterKey.R,
                        global::Godot.Key.Right=>ECounterKey.Right,global::Godot.Key.Up=>ECounterKey.Up,global::Godot.Key.Plus=>ECounterKey.Plus,
                        global::Godot.Key.Equal when key.ShiftPressed=>ECounterKey.Plus,global::Godot.Key.KpAdd=>ECounterKey.Add,_=>ECounterKey.Other});break;
            }
        }
        catch(Exception error){Fail(error);}
    }
    private async Task Verify()
    {
        try
        {
            Directory.CreateDirectory(_output);
            byte[] before=await _target!.ReadbackRgbaAsync();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            SaveImage(before,"counter-before.png");
            _demo!.KeyDown(ECounterKey.Plus);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            byte[] after=await _target.ReadbackRgbaAsync();SaveImage(after,"counter-after.png");
            if(before.Length==0||before.AsSpan().SequenceEqual(after))throw new InvalidOperationException("Counter key action did not change the source-rendered pixels.");
            _demo.KeyDown(ECounterKey.R);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            byte[] reset=await _target.ReadbackRgbaAsync();SaveImage(reset,"counter-reset.png");
            if(!before.AsSpan().SequenceEqual(reset))throw new InvalidOperationException("Counter reset did not restore the baseline pixels.");
            string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            File.WriteAllText(Path.Combine(_output,"host-verification.json"),System.Text.Json.JsonSerializer.Serialize(new{
                backend="Godot main RenderingDevice",before=Hash(before),after=Hash(after),reset=Hash(reset),key_changes_pixels=true,reset_restores_pixels=true
            },new System.Text.Json.JsonSerializerOptions(){WriteIndented=true}));
            GD.Print("SKRGUI_GODOT_HOST_CHECKS: passed");GetTree().Quit();
        }
        catch(Exception error){Fail(error);}
    }
    private void SaveImage(byte[] rgba,string name)
    {
        var size=_target!.PixelSize();using var image=Image.CreateFromData(size.Width,size.Height,false,Image.Format.Rgba8,rgba);
        Error result=image.SavePng(Path.Combine(_output,name));if(result!=Error.Ok)throw new IOException("Cannot save validation frame: "+result);
    }
    private void Fail(Exception error)
    {
        _failed=true;GD.PushError(error.ToString());if(_verifying)GetTree().Quit(1);
    }
    public override void _ExitTree()
    {
        _ready=false;
        _demo?.Dispose();_demo=null;
        _screen?.QueueFree();_screen=null;
        _target?.Dispose();_target=null;
        _backend?.Dispose();_backend=null;
    }
}
