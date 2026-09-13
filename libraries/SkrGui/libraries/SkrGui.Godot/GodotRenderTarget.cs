using G = global::Godot;
namespace SkrGui.Godot;

// GalleryRenderTarget ownership translated to the shared Godot RenderingDevice.
public sealed class GodotRenderTarget : RenderTarget
{
    private readonly GodotVisualBackend _backend;
    private Sizei _pixelSize;
    private bool _disposed;
    private readonly TargetTexture _texture;
    public G.Texture2Drd DisplayTexture { get; } = new();
    internal G.Rid ColorRid, FramebufferRid;
    internal Sizei GpuPixelSize;
    internal GodotRenderTarget(GodotVisualBackend backend, Sizei size)
    { _backend=backend; _pixelSize=size; _texture=new(this); }
    public override VisualBackend VisualBackend()=>_backend;
    public override Texture Texture()=>_texture;
    public override Sizei PixelSize()=>_pixelSize;
    public void Resize(Sizei size)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(size.Width<=0||size.Height<=0)throw new ArgumentOutOfRangeException(nameof(size));
        _pixelSize=size;
    }
    public Task<byte[]> ReadbackRgbaAsync()=>_backend.ReadbackRgbaAsync(this);
    public override void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _backend.ReleaseTarget(this);
    }
    internal sealed class TargetTexture(GodotRenderTarget target):Texture
    {
        internal readonly GodotRenderTarget Target=target;
        public override Sizei PixelSize()=>Target.PixelSize();
    }
}
