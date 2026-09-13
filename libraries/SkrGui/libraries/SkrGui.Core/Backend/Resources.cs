namespace SkrGui;

// Sources: visual/backend/{texture,shader,render_target}.hpp at 611561f8.
public abstract class Texture : IDisposable
{
    public abstract Sizei PixelSize();
    public virtual void Dispose() { }
}

public abstract class Shader : IDisposable
{
    public virtual void Dispose() { }
}

public abstract class RenderTarget : IDisposable
{
    public abstract VisualBackend VisualBackend();
    public abstract Texture? Texture();
    public abstract Sizei PixelSize();
    public virtual void Dispose() { }
}
