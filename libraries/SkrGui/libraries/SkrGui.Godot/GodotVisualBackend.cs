using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SkrGui.Gallery;
using G = global::Godot;
using RD = global::Godot.RenderingDevice;
namespace SkrGui.Godot;

// Source: gallery_common/src/gallery_renderer.cpp @ 611561f8.
// CPU draw construction and effect ordering are retained. Godot owns submission,
// barriers and lifetime of the shared Vulkan device; no SkrGui business runs natively.
public sealed class GodotVisualBackend : VisualBackend
{
    private sealed class Draw
    {
        public bool Backdrop;public uint FirstIndex,IndexCount;
        public Texture? Texture;public GalleryBackendShader Shader=null!;
        public Rectf Clip=Rectf.Largest(),Capture;
    }
    private sealed class Frame
    {
        public byte[] Vertices=[];public uint VertexCount;public uint[] Indices=[];
        public List<Draw> Draws=[];public Dictionary<GalleryBackendTexture,GalleryTextureData> Textures=[];
        public List<TextServices> TextServices=[];
        public GodotRenderTarget? Target;public Sizei LogicSize,PixelSize;public Recti Viewport;public Recti? Scissor;
        public bool Clear;public SRGBColor ClearColor;
    }
    private sealed class TextureGpu { public G.Rid Rid,GrayRid;public GalleryTextureData Data=null!; }
    private sealed class ProgramGpu { public G.Rid Shader;public Dictionary<long,G.Rid> Pipelines=[]; }
    private readonly GalleryBackendResources _resources;
    private readonly List<Rectf> _clips=[];
    private readonly HashSet<GodotRenderTarget> _targets=[];
    private readonly Dictionary<GalleryBackendTexture,TextureGpu> _textures=[];
    private readonly Dictionary<GalleryBackendShader,ProgramGpu> _programs=[];
    private Frame _frame=new();private bool _frameOpen,_targetOpen,_disposed;
    private RD? _device;private G.Rid _sampler,_blurShader,_blurPipeline,_readbackShader,_readbackPipeline;
    private long _vertexFormat;
    private readonly List<(G.Rid Rid,bool Uniform)> _frameGpu=[];
    private G.Rid _capture,_down,_ping;private Sizei _captureSize,_downSize;
    public Exception? LastRenderError { get; private set; }
    public GalleryBackendResources Resources=>_resources;
    public GodotVisualBackend(GalleryBackendResources? resources=null)=>_resources=resources??new();
    public GodotRenderTarget? CreateTarget(Sizei pixelSize)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(pixelSize.Width<=0||pixelSize.Height<=0)return null;
        var target=new GodotRenderTarget(this,pixelSize);_targets.Add(target);return target;
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private Rectf CurrentClip()=>_clips.Count==0?Rectf.Largest():_clips[^1];
    public override bool BeginFrame()
    {
        if(_disposed||_frameOpen||G.RenderingServer.GetRenderingDevice()==null)return false;
        _frame=new();_clips.Clear();_frameOpen=true;_targetOpen=false;return true;
    }
    public override void UpdateVertexBuffer(ReadOnlySpan<MeshVertex> vertices)
    {
        Require(_frameOpen,"Vertex updates require an open backend frame");
        _frame.Vertices=new byte[checked(vertices.Length*20)];_frame.VertexCount=(uint)vertices.Length;
        for(int i=0;i<vertices.Length;i++)
        {
            ref readonly var v=ref vertices[i];int p=i*20;
            Float(_frame.Vertices,p,v.Pos.X);Float(_frame.Vertices,p+4,v.Pos.Y);Float(_frame.Vertices,p+8,v.Uv.X);Float(_frame.Vertices,p+12,v.Uv.Y);
            UInt(_frame.Vertices,p+16,GalleryShared.PackMeshColor(v.PackedColor,false));
        }
    }
    public override void UpdateIndexBuffer(ReadOnlySpan<uint> indices)
    {Require(_frameOpen,"Index updates require an open backend frame");_frame.Indices=indices.ToArray();}
    public override void BeginRenderTarget(in VisualRenderTargetDesc desc)
    {
        Require(_frameOpen&&!_targetOpen,"Render target nesting is invalid");
        Require(desc.Target is GodotRenderTarget&&ReferenceEquals(desc.Target.VisualBackend(),this),"Render target belongs to another backend");
        _frame.Target=(GodotRenderTarget)desc.Target!;
        _frame.LogicSize=new(Math.Max(1,(int)MathF.Ceiling(desc.LogicSize.Width)),Math.Max(1,(int)MathF.Ceiling(desc.LogicSize.Height)));
        _frame.PixelSize=_frame.Target.PixelSize();
        _frame.Viewport=desc.Viewport.IsEmpty()?Recti.OffsetSize(Offseti.Zero(),_frame.PixelSize):desc.Viewport;
        _frame.Scissor=desc.Scissor;_frame.Clear=desc.Clear;_frame.ClearColor=desc.ClearColor;_targetOpen=true;
    }
    public override void BeginClip(in VisualClipDraw clip)
    {
        Require(_targetOpen,"Clip commands require an open render target");
        // The reference gallery renderer deliberately ignores clip.Kind/Range.
        _clips.Add(CurrentClip().Intersect(clip.Bound));
    }
    public override void EndClip()
    {Require(_clips.Count>0,"Clip stack underflow");if(_clips.Count>0)_clips.RemoveAt(_clips.Count-1);}
    private bool ValidRange(VisualDrawRange range)=>!range.IsEmpty()&&range.IndexStart<=uint.MaxValue&&range.IndexCount<=uint.MaxValue&&range.IndexStart+range.IndexCount<=(ulong)_frame.Indices.Length;
    private void Append(Shader? shader,Texture? texture,Rectf clip,VisualDrawRange range)
    {
        shader??=texture!=null?_resources.TexturedShader():_resources.SolidColorShader();
        Require(shader is GalleryBackendShader,"Godot gallery backend requires a GalleryBackendShader");
        if(texture is GalleryBackendTexture t)
        {
            _resources.AddTexture(t);
            if(!_frame.Textures.ContainsKey(t))_frame.Textures.Add(t,t.Data);
        }
        else Require(texture==null||texture is GodotRenderTarget.TargetTexture,"Texture belongs to a different backend");
        if(_frame.Draws.Count>0)
        {
            var last=_frame.Draws[^1];
            if(!last.Backdrop&&ReferenceEquals(last.Shader,shader)&&ReferenceEquals(last.Texture,texture)&&last.Clip==clip&&last.FirstIndex+last.IndexCount==(uint)range.IndexStart)
            {last.IndexCount+=(uint)range.IndexCount;return;}
        }
        _frame.Draws.Add(new(){FirstIndex=(uint)range.IndexStart,IndexCount=(uint)range.IndexCount,Texture=texture,Shader=(GalleryBackendShader)shader,Clip=clip});
    }
    public override void DrawMesh(in VisualMeshDraw draw)
    {Require(_targetOpen,"Mesh draws require an open render target");if(ValidRange(draw.Range))Append(draw.Shader,draw.Texture,CurrentClip(),draw.Range);}
    private GalleryBackendTexture? TextAtlas(TextServices services,uint index)
    {
        var atlas=services.Atlas(index);ulong count=(ulong)atlas.Stride*atlas.Height;
        if(atlas.AtlasIndex!=index||atlas.Width==0||atlas.Height==0||atlas.Stride==0||(ulong)atlas.Pixels.Length<count)return null;
        var texture=_resources.FontAtlasBackendTexture(services,index);var old=texture.Data;
        if(old.Generation!=atlas.Generation||old.Width!=atlas.Width||old.Height!=atlas.Height||old.Stride!=atlas.Stride||old.Format!=atlas.Format)
        {
            texture.ResetData(new(){Width=atlas.Width,Height=atlas.Height,Stride=atlas.Stride,Generation=atlas.Generation,Format=atlas.Format,Pixels=atlas.Pixels.Span[..checked((int)count)].ToArray()});
        }
        return texture;
    }
    public override void DrawText(in VisualTextDraw draw)
    {
        Require(_targetOpen,"Text draws require an open render target");
        if(draw.TextServices==null||draw.AtlasIndex==uint.MaxValue||!ValidRange(draw.Range))return;
        var texture=TextAtlas(draw.TextServices,draw.AtlasIndex);if(texture==null)return;
        _frame.TextServices.Add(draw.TextServices);Append(_resources.TextPixelModeShader(draw.PixelMode),texture,CurrentClip(),draw.Range);
    }
    public override void DrawAtlas(in VisualAtlasDraw draw)
    {
        Require(_targetOpen,"Atlas draws require an open render target");
        if(draw.TextServices==null||draw.AtlasIndex==uint.MaxValue||!ValidRange(draw.Range))return;
        var texture=TextAtlas(draw.TextServices,draw.AtlasIndex);if(texture==null)return;
        _frame.TextServices.Add(draw.TextServices);Append(_resources.TexturedShader(),texture,CurrentClip(),draw.Range);
    }
    public override void DrawBackdrop(in VisualBackdropDraw draw)
    {
        Require(_targetOpen,"Backdrop draws require an open render target");
        Require(draw.Command is BatchCmdBackdropGlass,"Gallery backend only supports gallery glass backdrops");
        if(draw.Command is not BatchCmdBackdropGlass b||!ValidRange(draw.Range))return;
        var shape=new GalleryBackdropShapeParams(){Size=b.ShapeSize,TopLeft=b.TopLeft,TopRight=b.TopRight,BottomRight=b.BottomRight,BottomLeft=b.BottomLeft};
        _frame.Draws.Add(new(){Backdrop=true,FirstIndex=(uint)draw.Range.IndexStart,IndexCount=(uint)draw.Range.IndexCount,Clip=CurrentClip(),Capture=draw.Bound.Intersect(CurrentClip()),
            Shader=_resources.BackdropBlurShader(b.BlurRadius,b.LightDirection,b.LightColor,b.GlassColor,b.GlassTransmittance,b.GlassGrain,b.GlassRefraction,shape)});
    }
    public override void EndRenderTarget()
    {Require(_targetOpen&&_clips.Count==0,"Render target ended with an invalid clip stack");_targetOpen=false;}
    public override bool EndFrame()
    {
        if(!_frameOpen||_targetOpen||_frame.Target==null)return false;
        var submitted=_frame;_frameOpen=false;
        // Match source retention until its submitted GPU frame is consumed.
        bool accepted=true;
        RenderThread(()=>{try{Render(submitted);LastRenderError=null;}catch(Exception e){accepted=false;LastRenderError=e;G.GD.PushError(e.ToString());}});
        return accepted;
    }
    internal static void RenderThread(Action action)=>G.RenderingServer.CallOnRenderThread(G.Callable.From(action));
    private RD Device=>_device??throw new InvalidOperationException("RenderingDevice not initialized");
    private static void Float(byte[] bytes,int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset,4),value);
    private static void UInt(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    private void Free(G.Rid rid){if(rid.IsValid)Device.FreeRid(rid);}
    private G.Rid FrameResource(G.Rid rid,bool uniform=false){Require(rid.IsValid,"Godot failed to allocate a frame GPU resource");_frameGpu.Add((rid,uniform));return rid;}
    private void ReleaseFrameResources()
    {
        // RD frees dependent arrays/uniform sets when their owning buffer/texture
        // dies. Reverse creation order and validate texture-dependent sets.
        for(int i=_frameGpu.Count-1;i>=0;i--){var resource=_frameGpu[i];if(!resource.Uniform||Device.UniformSetIsValid(resource.Rid))Free(resource.Rid);}
        _frameGpu.Clear();
    }
    private G.Rid Compile(string vertex,string fragment)
    {
        using var source=new G.RDShaderSource(){Language=RD.ShaderLanguage.Glsl,SourceVertex=vertex,SourceFragment=fragment};
        using var spirv=Device.ShaderCompileSpirVFromSource(source);
        Require(spirv.CompileErrorVertex.Length==0&&spirv.CompileErrorFragment.Length==0,spirv.CompileErrorVertex+spirv.CompileErrorFragment);
        var rid=Device.ShaderCreateFromSpirV(spirv);Require(rid.IsValid,"Failed to create SkrGui render shader");return rid;
    }
    private G.Rid CompileCompute(string code)
    {
        using var source=new G.RDShaderSource(){Language=RD.ShaderLanguage.Glsl,SourceCompute=code};using var spirv=Device.ShaderCompileSpirVFromSource(source);
        Require(spirv.CompileErrorCompute.Length==0,spirv.CompileErrorCompute);var rid=Device.ShaderCreateFromSpirV(spirv);Require(rid.IsValid,"Failed to create SkrGui compute shader");return rid;
    }
    private void InitializeGpu()
    {
        if(_device!=null)return;
        _device=G.RenderingServer.GetRenderingDevice();Require(_device!=null,"SkrGui requires Godot Forward+ or Mobile RenderingDevice");
        _vertexFormat=Device.VertexFormatCreate(new G.Collections.Array<G.RDVertexAttribute>{
            new(){Location=0,Binding=0,Format=RD.DataFormat.R32G32Sfloat,Offset=0,Stride=20},
            new(){Location=1,Binding=0,Format=RD.DataFormat.R32G32Sfloat,Offset=8,Stride=20},
            new(){Location=2,Binding=0,Format=RD.DataFormat.R8G8B8A8Unorm,Offset=16,Stride=20}});
        _sampler=Device.SamplerCreate(new(){MagFilter=RD.SamplerFilter.Linear,MinFilter=RD.SamplerFilter.Linear,MipFilter=RD.SamplerFilter.Linear,
            RepeatU=RD.SamplerRepeatMode.ClampToEdge,RepeatV=RD.SamplerRepeatMode.ClampToEdge,RepeatW=RD.SamplerRepeatMode.ClampToEdge,EnableCompare=false,CompareOp=RD.CompareOperator.Never});
        _blurShader=CompileCompute(ShaderSources.BlurCompute());_blurPipeline=Device.ComputePipelineCreate(_blurShader);
        _readbackShader=CompileCompute(ShaderSources.ReadbackCompute());_readbackPipeline=Device.ComputePipelineCreate(_readbackShader);
    }
    internal void EnsureTarget(GodotRenderTarget target,Sizei size)
    {
        InitializeGpu();if(target.ColorRid.IsValid&&target.GpuPixelSize==size)return;
        Free(target.FramebufferRid);Free(target.ColorRid);
        target.ColorRid=CreateTexture(size,RD.DataFormat.R8G8B8A8Unorm,RD.TextureUsageBits.ColorAttachmentBit|RD.TextureUsageBits.SamplingBit|RD.TextureUsageBits.CanCopyFromBit|RD.TextureUsageBits.CanCopyToBit);
        target.FramebufferRid=Device.FramebufferCreate(new(){target.ColorRid});Require(target.FramebufferRid.IsValid,"Failed to create SkrGui framebuffer");
        target.GpuPixelSize=size;target.DisplayTexture.TextureRdRid=target.ColorRid;
    }
    private G.Rid CreateTexture(Sizei size,RD.DataFormat format,RD.TextureUsageBits usage,byte[]? data=null)
    {
        using var f=new G.RDTextureFormat(){Format=format,Width=(uint)size.Width,Height=(uint)size.Height,Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RD.TextureType.Type2D,Samples=RD.TextureSamples.Samples1,UsageBits=usage};
        using var view=new G.RDTextureView();var rid=Device.TextureCreate(f,view,data==null?null:new G.Collections.Array<byte[]>{data});Require(rid.IsValid,"Failed to create SkrGui texture");return rid;
    }
    private G.Rid Texture(GalleryBackendTexture texture,GalleryTextureData data)
    {
        if(_textures.TryGetValue(texture,out var cached)&&ReferenceEquals(cached.Data,data))return cached.Rid;
        if(cached!=null){Free(cached.GrayRid);Free(cached.Rid);}
        int channels=data.Format==ETextAtlasFormat.L8?1:4;int row=checked((int)data.Width*channels);
        Require(data.Width>0&&data.Height>0&&data.Stride>=row&&(ulong)data.Pixels.Length>=(ulong)data.Stride*data.Height,"Invalid texture dimensions, row stride or pixel data");
        byte[] packed=data.Pixels;if(data.Stride!=row){packed=new byte[checked(row*(int)data.Height)];for(int y=0;y<data.Height;y++)data.Pixels.AsSpan(checked((int)data.Stride*y),row).CopyTo(packed.AsSpan(row*y));}
        var rid=CreateTexture(new((int)data.Width,(int)data.Height),channels==1?RD.DataFormat.R8Unorm:RD.DataFormat.R8G8B8A8Unorm,RD.TextureUsageBits.SamplingBit|RD.TextureUsageBits.CanUpdateBit,packed);
        G.Rid gray=default;
        if(channels==1){using var view=new G.RDTextureView{SwizzleR=RD.TextureSwizzle.R,SwizzleG=RD.TextureSwizzle.R,SwizzleB=RD.TextureSwizzle.R,SwizzleA=RD.TextureSwizzle.R};gray=Device.TextureCreateShared(view,rid);Require(gray.IsValid,"Failed to create L8 gray-alpha texture view");}
        _textures[texture]=new(){Rid=rid,GrayRid=gray,Data=data};texture.ReleaseGpu=()=>RenderThread(()=>{if(_textures.Remove(texture,out var resource)){Free(resource.GrayRid);Free(resource.Rid);}});return rid;
    }
    private ProgramGpu Program(GalleryBackendShader shader)
    {
        if(_programs.TryGetValue(shader,out var program))return program;
        var kind=shader.Kind;int effect=kind switch{EGalleryShaderKind.TextGrayLcd=>1,EGalleryShaderKind.TextSdf=>2,EGalleryShaderKind.TextSdfLcd=>3,_=>0};
        program=new(){Shader=kind==EGalleryShaderKind.BackdropBlur?Compile(ShaderSources.BackdropVertex(),ShaderSources.BackdropFragment()):Compile(ShaderSources.GalleryVertex(kind!=EGalleryShaderKind.Solid,effect),ShaderSources.GalleryFragment(kind!=EGalleryShaderKind.Solid,effect))};
        _programs.Add(shader,program);bool alive=true;shader.GpuValidity=()=>alive;
        shader.ReleaseGpu=()=>{alive=false;RenderThread(()=>{if(_programs.Remove(shader,out var removed)){foreach(var pipeline in removed.Pipelines.Values)Free(pipeline);Free(removed.Shader);}});};
        return program;
    }
    private G.Rid Pipeline(GalleryBackendShader shader,long framebufferFormat)
    {
        var program=Program(shader);
        if(!program.Pipelines.TryGetValue(framebufferFormat,out var pipeline))
        {
            bool lcd=shader.Kind is EGalleryShaderKind.TextGrayLcd or EGalleryShaderKind.TextSdfLcd;
            using var attachment=new G.RDPipelineColorBlendStateAttachment(){EnableBlend=true,SrcColorBlendFactor=RD.BlendFactor.One,DstColorBlendFactor=lcd?RD.BlendFactor.OneMinusSrc1Color:RD.BlendFactor.OneMinusSrcAlpha,
                SrcAlphaBlendFactor=RD.BlendFactor.One,DstAlphaBlendFactor=lcd?RD.BlendFactor.OneMinusSrc1Alpha:RD.BlendFactor.OneMinusSrcAlpha,ColorBlendOp=RD.BlendOperation.Add,AlphaBlendOp=RD.BlendOperation.Add,WriteR=true,WriteG=true,WriteB=true,WriteA=true};
            using var blend=new G.RDPipelineColorBlendState(){Attachments=new(){attachment}};
            using var raster=new G.RDPipelineRasterizationState(){CullMode=RD.PolygonCullMode.Disabled};
            using var multisample=new G.RDPipelineMultisampleState(){SampleCount=RD.TextureSamples.Samples1};using var depth=new G.RDPipelineDepthStencilState();
            pipeline=Device.RenderPipelineCreate(program.Shader,framebufferFormat,_vertexFormat,RD.RenderPrimitive.Triangles,raster,multisample,depth,blend);
            Require(pipeline.IsValid,"Failed to create SkrGui "+shader.Kind+" pipeline");program.Pipelines.Add(framebufferFormat,pipeline);
        }
        return pipeline;
    }
    private G.RDUniform SampledUniform(uint binding,G.Rid texture)
    {var u=new G.RDUniform(){UniformType=RD.UniformType.SamplerWithTexture,Binding=(int)binding};u.AddId(_sampler);u.AddId(texture);return u;}
    private G.RDUniform SingleUniform(RD.UniformType type,uint binding,G.Rid rid)
    {var u=new G.RDUniform(){UniformType=type,Binding=(int)binding};u.AddId(rid);return u;}
    private G.Rid UniformSet(G.Rid shader,params G.RDUniform[] uniforms)
    {var set=FrameResource(Device.UniformSetCreate(new(uniforms),shader,0),true);foreach(var u in uniforms)u.Dispose();return set;}
    private static Rectf LogicViewport(Frame f)=>Rectf.LTWH(0,0,f.LogicSize.Width,f.LogicSize.Height);
    private static Rectf PixelRect(Frame f,Rectf rect)
    {
        var clipped=rect.Intersect(LogicViewport(f));if(clipped.IsEmpty())return Rectf.Zero();
        float sx=(float)f.Viewport.Width()/f.LogicSize.Width,sy=(float)f.Viewport.Height()/f.LogicSize.Height;
        return new(f.Viewport.Left+clipped.Left*sx,f.Viewport.Top+clipped.Top*sy,f.Viewport.Left+clipped.Right*sx,f.Viewport.Top+clipped.Bottom*sy);
    }
    private bool Scissor(long list,Frame f,Draw draw)
    {
        var clip=PixelRect(f,draw.Clip);if(f.Scissor is {} s)clip=clip.Intersect(new(s.Left,s.Top,s.Right,s.Bottom));
        if(clip.IsEmpty())return false;
        int l=Math.Max(0,(int)MathF.Floor(clip.Left)),t=Math.Max(0,(int)MathF.Floor(clip.Top)),r=Math.Min(f.PixelSize.Width,(int)MathF.Ceiling(clip.Right)),b=Math.Min(f.PixelSize.Height,(int)MathF.Ceiling(clip.Bottom));
        if(l>=r||t>=b)return false;Device.DrawListEnableScissor(list,new G.Rect2(l,t,r-l,b-t));return true;
    }
    private static void Projection(byte[] bytes,Frame f,bool backdrop=false)
    {
        // Vulkan's framebuffer Y convention replaces CGPU's API normalization.
        // RD has a full-target viewport; incorporate the source viewport into NDC.
        var viewport=backdrop?Recti.OffsetSize(Offseti.Zero(),f.PixelSize):f.Viewport;
        Float(bytes,0,2f*viewport.Width()/f.LogicSize.Width/f.PixelSize.Width);
        Float(bytes,20,2f*viewport.Height()/f.LogicSize.Height/f.PixelSize.Height);
        Float(bytes,40,1);Float(bytes,48,2f*viewport.Left/f.PixelSize.Width-1);Float(bytes,52,2f*viewport.Top/f.PixelSize.Height-1);Float(bytes,60,1);
    }
    private long BeginPass(Frame f,bool clear)
    {return Device.DrawListBegin(f.Target!.FramebufferRid,clear?RD.DrawFlags.ClearColor0:RD.DrawFlags.DefaultAll,clear?new[]{new G.Color(f.ClearColor.R,f.ClearColor.G,f.ClearColor.B,f.ClearColor.A)}:[]);}
    private void BindGeometry(long list,G.Rid vertexArray,G.Rid indexBuffer,Draw draw)
    {
        Device.DrawListBindVertexArray(list,vertexArray);
        var indexArray=FrameResource(Device.IndexArrayCreate(indexBuffer,draw.FirstIndex,draw.IndexCount));Device.DrawListBindIndexArray(list,indexArray);
    }
    private void Render(Frame f)
    {
        InitializeGpu();ReleaseFrameResources();
        Require(f.Target!=null,"Frame has no render target");EnsureTarget(f.Target!,f.PixelSize);
        long format=Device.FramebufferGetFormat(f.Target!.FramebufferRid);
        G.Rid vertexArray=default,indexBuffer=default;
        if(f.Vertices.Length!=0&&f.Indices.Length!=0)
        {
            var vb=FrameResource(Device.VertexBufferCreate((uint)f.Vertices.Length,f.Vertices));
            indexBuffer=FrameResource(Device.IndexBufferCreate((uint)f.Indices.Length,RD.IndexBufferFormat.Uint32,MemoryMarshal.AsBytes(f.Indices.AsSpan()).ToArray()));
            vertexArray=FrameResource(Device.VertexArrayCreate(f.VertexCount,_vertexFormat,new(){vb}));
        }
        foreach(var (texture,data) in f.Textures)Texture(texture,data);
        long list=BeginPass(f,f.Clear);
        foreach(var draw in f.Draws)
        {
            if(!vertexArray.IsValid)continue;
            if(draw.Backdrop)
            {
                Device.DrawListEnd();Backdrop(f,draw,vertexArray,indexBuffer,format);list=BeginPass(f,false);continue;
            }
            if(!Scissor(list,f,draw))continue;
            var shader=draw.Shader;bool textured=shader.IsTextured();G.Rid texture=default;Sizei textureSize=new(1,1);
            if(textured)
            {
                if(draw.Texture is GalleryBackendTexture t&&f.Textures.TryGetValue(t,out var data)){texture=Texture(t,data);if(shader.Kind==EGalleryShaderKind.Textured&&data.Format==ETextAtlasFormat.L8&&_textures.TryGetValue(t,out var gpu))texture=gpu.GrayRid;textureSize=new((int)Math.Max(1,data.Width),(int)Math.Max(1,data.Height));}
                else if(draw.Texture is GodotRenderTarget.TargetTexture target){texture=target.Target.ColorRid;textureSize=target.PixelSize();}
                if(!texture.IsValid)continue;
            }
            Device.DrawListBindRenderPipeline(list,Pipeline(shader,format));BindGeometry(list,vertexArray,indexBuffer,draw);
            var constants=new byte[80];Projection(constants,f);Float(constants,64,textureSize.Width);Float(constants,68,textureSize.Height);Float(constants,72,1);
            Device.DrawListSetPushConstant(list,constants,80);
            if(textured)Device.DrawListBindUniformSet(list,UniformSet(Program(shader).Shader,SampledUniform(0,texture)),0);
            Device.DrawListDraw(list,true,1);
        }
        Device.DrawListEnd();
    }
    private void EnsureBackdrop(Sizei size,float scale)
    {
        var down=new Sizei(Math.Max(1,(int)MathF.Ceiling(size.Width/scale)),Math.Max(1,(int)MathF.Ceiling(size.Height/scale)));
        if(_capture.IsValid&&_captureSize==size&&_downSize==down)return;
        Free(_capture);Free(_down);Free(_ping);_captureSize=size;_downSize=down;
        _capture=CreateTexture(size,RD.DataFormat.R8G8B8A8Unorm,RD.TextureUsageBits.SamplingBit|RD.TextureUsageBits.CanCopyToBit);
        _down=CreateTexture(down,RD.DataFormat.R8G8B8A8Unorm,RD.TextureUsageBits.SamplingBit|RD.TextureUsageBits.StorageBit);
        _ping=CreateTexture(down,RD.DataFormat.R8G8B8A8Unorm,RD.TextureUsageBits.SamplingBit|RD.TextureUsageBits.StorageBit);
    }
    private void BlurPass(G.Rid source,G.Rid destination,uint dw,uint dh,Sizei sourceSize,Offsetf origin,float radius,float downscale,uint mode)
    {
        var constants=new byte[48];UInt(constants,0,dw);UInt(constants,4,dh);UInt(constants,8,(uint)sourceSize.Width);UInt(constants,12,(uint)sourceSize.Height);
        Float(constants,16,origin.X);Float(constants,20,origin.Y);Float(constants,24,radius);Float(constants,28,downscale);UInt(constants,32,mode);
        var uniforms=UniformSet(_blurShader,SampledUniform(0,source),SingleUniform(RD.UniformType.Image,1,destination));
        long list=Device.ComputeListBegin();Device.ComputeListBindComputePipeline(list,_blurPipeline);Device.ComputeListBindUniformSet(list,uniforms,0);Device.ComputeListSetPushConstant(list,constants,48);
        Device.ComputeListDispatch(list,(dw+7)/8,(dh+7)/8,1);Device.ComputeListEnd();
    }
    private void Backdrop(Frame f,Draw draw,G.Rid vertexArray,G.Rid indexBuffer,long format)
    {
        var logic=draw.Capture.Intersect(LogicViewport(f));var pixel=PixelRect(f,draw.Capture);if(logic.IsEmpty()||pixel.IsEmpty())return;
        var shader=draw.Shader;float radius=MathF.Max(0,shader.BlurRadius)*MathF.Max((float)f.Viewport.Width()/f.LogicSize.Width,(float)f.Viewport.Height()/f.LogicSize.Height);
        float downscale=MathF.Max(1,shader.BlurDownScale);EnsureBackdrop(f.PixelSize,downscale);
        uint dw=Math.Max(1,(uint)MathF.Ceiling(pixel.Width()/downscale)),dh=Math.Max(1,(uint)MathF.Ceiling(pixel.Height()/downscale));
        Require(dw<=_downSize.Width&&dh<=_downSize.Height,"Backdrop exceeds its downsample texture");
        var result=Device.TextureCopy(f.Target!.ColorRid,_capture,G.Vector3.Zero,G.Vector3.Zero,new(f.PixelSize.Width,f.PixelSize.Height,1),0,0,0,0);Require(result==G.Error.Ok,"Backdrop background copy failed");
        BlurPass(_capture,_down,dw,dh,_captureSize,new(pixel.Left,pixel.Top),radius,downscale,0);
        if(radius/downscale>.0001f){BlurPass(_down,_ping,dw,dh,new((int)dw,(int)dh),Offsetf.Zero(),radius,downscale,1);BlurPass(_ping,_down,dw,dh,new((int)dw,(int)dh),Offsetf.Zero(),radius,downscale,2);}
        var constants=new byte[208];Projection(constants,f,true);
        Float(constants,64,(float)dw/Math.Max(_downSize.Width,1));Float(constants,68,(float)dh/Math.Max(_downSize.Height,1));
        Float(constants,80,logic.Left);Float(constants,84,logic.Top);Float(constants,88,MathF.Max(1,logic.Width()));Float(constants,92,MathF.Max(1,logic.Height()));
        var shape=shader.BackdropShape;Float(constants,96,MathF.Max(1,shape.Size.Width));Float(constants,100,MathF.Max(1,shape.Size.Height));Float(constants,104,shader.LightDirection.X);Float(constants,108,shader.LightDirection.Y);
        var lc=shader.LightColor;Float(constants,112,lc.R);Float(constants,116,lc.G);Float(constants,120,lc.B);Float(constants,124,lc.A);
        var gc=shader.GlassColor;Float(constants,128,gc.R);Float(constants,132,gc.G);Float(constants,136,gc.B);Float(constants,140,gc.A);
        Float(constants,144,shader.GlassTransmittance);Float(constants,148,shader.GlassGrain);Float(constants,152,shader.GlassRefraction);
        Float(constants,160,MathF.Min(.45f,2f/dw));Float(constants,164,MathF.Min(.45f,2f/dh));
        Float(constants,176,shape.TopLeft.X);Float(constants,180,shape.TopLeft.Y);Float(constants,184,shape.TopRight.X);Float(constants,188,shape.TopRight.Y);
        Float(constants,192,shape.BottomRight.X);Float(constants,196,shape.BottomRight.Y);Float(constants,200,shape.BottomLeft.X);Float(constants,204,shape.BottomLeft.Y);
        var ubo=FrameResource(Device.UniformBufferCreate(208,constants));var set=UniformSet(Program(shader).Shader,SampledUniform(0,_down),SingleUniform(RD.UniformType.UniformBuffer,1,ubo));
        long list=BeginPass(f,false);
        if(Scissor(list,f,draw)){Device.DrawListBindRenderPipeline(list,Pipeline(shader,format));BindGeometry(list,vertexArray,indexBuffer,draw);Device.DrawListBindUniformSet(list,set,0);Device.DrawListDraw(list,true,1);}
        Device.DrawListEnd();
    }
    public Task<byte[]> ReadbackRgbaAsync(GodotRenderTarget target)
    {
        Require(ReferenceEquals(target.VisualBackend(),this),"Readback target belongs to a different backend");
        var completion=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderThread(()=>
        {
            try
            {
                InitializeGpu();EnsureTarget(target,target.PixelSize());var size=target.GpuPixelSize;uint length=checked((uint)(size.Width*size.Height*4));
                var buffer=Device.StorageBufferCreate(length);var uniform0=SampledUniform(0,target.ColorRid);var uniform1=SingleUniform(RD.UniformType.StorageBuffer,1,buffer);
                var set=Device.UniformSetCreate(new(){uniform0,uniform1},_readbackShader,0);uniform0.Dispose();uniform1.Dispose();
                var constants=new byte[16];UInt(constants,0,(uint)size.Width);UInt(constants,4,(uint)size.Height);
                long list=Device.ComputeListBegin();Device.ComputeListBindComputePipeline(list,_readbackPipeline);Device.ComputeListBindUniformSet(list,set,0);Device.ComputeListSetPushConstant(list,constants,16);Device.ComputeListDispatch(list,(uint)(size.Width+7)/8,(uint)(size.Height+7)/8,1);Device.ComputeListEnd();
                var err=Device.BufferGetDataAsync(buffer,G.Callable.From<byte[]>(data=>{Free(set);Free(buffer);completion.TrySetResult(data);}),0,length);
                if(err!=G.Error.Ok){Free(set);Free(buffer);completion.TrySetException(new InvalidOperationException("Godot readback failed: "+err));}
            }
            catch(Exception e){completion.TrySetException(e);}
        });return completion.Task;
    }
    internal void ReleaseTarget(GodotRenderTarget target)
    {
        _targets.Remove(target);
        RenderThread(()=>{if(_device==null)return;target.DisplayTexture.TextureRdRid=default;Free(target.FramebufferRid);Free(target.ColorRid);target.FramebufferRid=target.ColorRid=default;});
    }
    public override void Dispose()
    {
        if(_disposed)return;_disposed=true;
        RenderThread(()=>
        {
            if(_device==null)return;ReleaseFrameResources();foreach(var target in _targets.ToArray())target.Dispose();_resources.Clear();foreach(var t in _textures.Values){Free(t.GrayRid);Free(t.Rid);}_textures.Clear();
            foreach(var p in _programs.Values){foreach(var pipeline in p.Pipelines.Values)Free(pipeline);Free(p.Shader);}_programs.Clear();
            Free(_capture);Free(_down);Free(_ping);Free(_sampler);Free(_blurPipeline);Free(_blurShader);Free(_readbackPipeline);Free(_readbackShader);
        });
    }
}
