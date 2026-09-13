using Godot;
using SkrGui;
using SkrGui.Gallery;
using SkrGui.Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// GPU conformance probes, independent of text shaping or widget layout.
public partial class BackendGpuProbe : Node
{
    private GodotVisualBackend? _backend;
    private GodotRenderTarget? _target;
    private readonly List<string> _passed=[];
    private string _output="";
    private Recti _testViewport;private Recti? _testScissor;
    private sealed record Layer(Rectf Rect,SRGBColor Color,GalleryBackendShader? Shader=null,SkrGui.Texture? Texture=null,Rectf? Clip=null,BatchCmdBackdropGlass? Glass=null,bool MeshClip=false);
    public override void _Ready(){_ = Verify();}
    private async Task Verify()
    {
        try
        {
            _output=ProjectSettings.GlobalizePath("res://../../artifacts/gpu-probe");Directory.CreateDirectory(_output);
            _backend=new();if(_backend.CreateTarget(new(0,0))!=null)throw new Exception("Empty target must be rejected");_target=_backend.CreateTarget(new(64,64));
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(!_backend.BeginFrame()||_backend.BeginFrame()||_backend.EndFrame())throw new Exception("Invalid frame retry state");
            _backend.BeginRenderTarget(new(){Target=_target,LogicSize=new(64,64),Clear=true,ClearColor=new(.25f,.5f,.75f,1)});_backend.EndRenderTarget();
            if(!_backend.EndFrame())throw new Exception("An empty frame must still clear the target");
            byte[] empty=await _target!.ReadbackRgbaAsync();Pixel(empty,5,5,64,128,191,255,1);Pass("failed frame attempts retain recording state; empty geometry still clears");
            byte[] solid=await Render(new(.1f,.2f,.3f,1),new Layer(new(8,12,28,28),new(1,0,0,1)));
            Pixel(solid,16,20,255,0,0,255);Pixel(solid,2,2,26,51,76,255,1);Save(solid,"solid.png");Pass("RGBA8 UNORM, vertex color unpack and top-left world-to-pixel coordinates");
            byte[] blend=await Render(new(.2f,.4f,.6f,1),new Layer(new(0,0,64,64),new(.5f,0,0,.5f)));
            Pixel(blend,30,30,153,51,77,255,1);Pass("premultiplied source-over color and alpha blend");
            byte[] clip=await Render(new(0,0,0,1),new Layer(new(0,0,64,64),new(0,1,0,1),Clip:new(10,12,25,28),MeshClip:true));
            Pixel(clip,16,20,0,255,0,255);Pixel(clip,5,20,0,0,0,255);Pass("source ClipMesh contract: intersected AABB scissor");
            _testViewport=Recti.LTWH(16,8,32,40);_testScissor=Recti.LTWH(20,10,10,15);
            byte[] viewport=await Render(new(0,0,0,1),new Layer(new(0,0,64,64),new(1,0,0,1)));
            Pixel(viewport,25,15,255,0,0,255);Pixel(viewport,18,15,0,0,0,255);Pixel(viewport,25,30,0,0,0,255);_testViewport=default;_testScissor=null;
            Pass("nonzero viewport and physical scissor preserve source pixel mapping");
            var colorTexture=_backend.Resources.CreateTexture();colorTexture.ResetData(new(){Width=2,Height=2,Stride=8,Format=ETextAtlasFormat.RGBA8,Pixels=[255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255]});
            byte[] rgba=await Render(new(0,0,0,1),new Layer(new(0,0,64,64),new(1,1,1,1),(GalleryBackendShader)_backend.Resources.TexturedShader(),colorTexture));
            Pixel(rgba,15,15,255,0,0,255);Pixel(rgba,48,15,0,255,0,255);Pixel(rgba,15,48,0,0,255,255);Pass("RGBA atlas upload, clamp and bilinear sampling");
            var lcdTexture=_backend.Resources.CreateTexture();lcdTexture.ResetData(new(){Width=1,Height=1,Stride=4,Format=ETextAtlasFormat.RGBA8,Pixels=[128,64,32,255]});
            byte[] lcd=await Render(new(.2f,.4f,.6f,1),new Layer(new(0,0,64,64),new(1,1,1,1),_backend.Resources.Shader(EGalleryShaderKind.TextGrayLcd),lcdTexture));
            Pixel(lcd,30,30,153,140,166,255,1);Save(lcd,"lcd.png");Pass("dual-source LCD independently blends red/green/blue coverage");
            var sdfTexture=_backend.Resources.CreateTexture();sdfTexture.ResetData(new(){Width=1,Height=1,Stride=1,Format=ETextAtlasFormat.L8,Pixels=[255]});
            var grayTexture=_backend.Resources.CreateTexture();grayTexture.ResetData(new(){Width=1,Height=1,Stride=1,Format=ETextAtlasFormat.L8,Pixels=[128]});
            byte[] gray=await Render(new(.2f,.4f,.6f,1),new Layer(new(0,0,64,64),new(1,1,1,1),_backend.Resources.Shader(EGalleryShaderKind.Textured),grayTexture));
            Pixel(gray,30,30,153,179,204,255,1);Pass("L8 direct text uses the original R-to-RGBA alpha view");
            grayTexture.ResetData(new(){Width=1,Height=1,Stride=4,Format=ETextAtlasFormat.RGBA8,Generation=2,Pixels=[0,255,0,255]});
            byte[] replaced=await Render(new(0,0,0,1),new Layer(new(0,0,64,64),new(1,1,1,1),_backend.Resources.Shader(EGalleryShaderKind.Textured),grayTexture));
            Pixel(replaced,30,30,0,255,0,255);Pass("ResetData recreates GPU storage and bindings across atlas generation/format");
            foreach(var kind in new[]{EGalleryShaderKind.TextSdf,EGalleryShaderKind.TextSdfLcd})
            {byte[] pixels=await Render(new(0,0,0,1),new Layer(new(0,0,64,64),new(1,1,1,1),_backend.Resources.Shader(kind),sdfTexture));Pixel(pixels,30,30,255,255,255,255);Pass(kind+" R8 atlas pipeline and derivative branch");}
            var checker=new List<Layer>();for(int y=0;y<8;y++)for(int x=0;x<8;x++)checker.Add(new(new(x*8,y*8,x*8+8,y*8+8),((x+y)&1)==0?new(1,1,1,1):new(0,0,0,1)));
            byte[] baseline=await Render(new(0,0,0,1),checker.ToArray());
            checker.Add(new(new(8,8,56,56),new(1,1,1,1),Glass:new(){BlurRadius=12,ShapeSize=new(48,48),GlassTransmittance=1,GlassColor=new(1,1,1,1),LightColor=new(0,0,0,0),LightDirection=new(-.70710677f,-.70710677f)}));
            byte[] glass=await Render(new(0,0,0,1),checker.ToArray());
            if(glass.AsSpan().SequenceEqual(baseline))throw new Exception("Backdrop compute produced no visible blur");
            Pixel(glass,1,1,255,255,255,255);int center=Index(27,27);if(glass[center]<30||glass[center]>225)throw new Exception("Backdrop did not mix checkerboard background");
            Save(glass,"glass.png");Pass("backdrop captures previous draws, downsamples and executes Gaussian H/V passes");
            File.WriteAllText(Path.Combine(_output,"results.json"),System.Text.Json.JsonSerializer.Serialize(new{device="Godot main Vulkan RenderingDevice",passed=_passed},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("SKRGUI_GPU_PROBE_PASS="+_passed.Count);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task<byte[]> Render(SRGBColor clear,params Layer[] layers)
    {
        var vertices=new List<MeshVertex>();var indices=new List<uint>();
        foreach(var layer in layers)
        {
            uint start=(uint)vertices.Count;var r=layer.Rect;uint c=layer.Color.ToRgba32();
            vertices.Add(new(r.TopLeft(),new(0,0),c));vertices.Add(new(r.TopRight(),new(1,0),c));vertices.Add(new(r.BottomRight(),new(1,1),c));vertices.Add(new(r.BottomLeft(),new(0,1),c));
            indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        if(!_backend!.BeginFrame())throw new Exception("BeginFrame rejected");
        _backend.UpdateVertexBuffer(vertices.ToArray());_backend.UpdateIndexBuffer(indices.ToArray());
        _backend.BeginRenderTarget(new(){Target=_target,LogicSize=new(64,64),Viewport=_testViewport,Scissor=_testScissor,Clear=true,ClearColor=clear});
        for(int i=0;i<layers.Length;i++)
        {
            var layer=layers[i];var range=new VisualDrawRange{IndexStart=(ulong)i*6,IndexCount=6};
            if(layer.Clip is {} clip)_backend.BeginClip(new(){Bound=clip,Kind=layer.MeshClip?EVisualClipKind.Mesh:EVisualClipKind.Rect});
            if(layer.Glass!=null)_backend.DrawBackdrop(new(){Range=range,Bound=layer.Rect.Inflate(layer.Glass.BlurRadius),Command=layer.Glass});
            else _backend.DrawMesh(new(){Range=range,Shader=layer.Shader,Texture=layer.Texture});
            if(layer.Clip!=null)_backend.EndClip();
        }
        _backend.EndRenderTarget();if(!_backend.EndFrame())throw new Exception("EndFrame rejected");
        var bytes=await _target!.ReadbackRgbaAsync();
        if(_backend.LastRenderError!=null)throw new Exception("GPU renderer failed",_backend.LastRenderError);
        return bytes;
    }
    private static int Index(int x,int y)=>(y*64+x)*4;
    private static void Pixel(byte[] data,int x,int y,int r,int g,int b,int a,int tolerance=0)
    {
        var expected=new[]{r,g,b,a};int p=Index(x,y);
        for(int c=0;c<4;c++)if(Math.Abs(data[p+c]-expected[c])>tolerance)throw new Exception($"Pixel ({x},{y}) channel {c}: expected {expected[c]}, actual {data[p+c]}");
    }
    private void Save(byte[] rgba,string name){using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,rgba);image.SavePng(Path.Combine(_output,name));}
    private void Pass(string message){_passed.Add(message);GD.Print("PASS "+message);}
    public override void _ExitTree(){_target?.Dispose();_backend?.Dispose();}
}
