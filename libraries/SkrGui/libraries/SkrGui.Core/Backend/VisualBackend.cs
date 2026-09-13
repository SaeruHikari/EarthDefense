namespace SkrGui;

// Source: visual/backend/visual_backend.hpp at 611561f8. The backend is the replacement seam.
public struct VisualPaintDesc
{
    public float PixelRatio = 1f;
    public float AaRadius = 1f;
    public bool ScaleOffsetOnly = true;
    public VisualPaintDesc() { }
}
public struct VisualRenderDesc
{
    public RenderTarget? Target;
    public Recti Viewport;
    public Recti? Scissor;
    public bool Clear;
    public SRGBColor ClearColor = new();
    public VisualRenderDesc() { }
}
public struct VisualRenderTargetDesc
{
    public RenderTarget? Target;
    public Sizef LogicSize;
    public Recti Viewport;
    public Recti? Scissor;
    public bool Clear;
    public SRGBColor ClearColor = new();
    public VisualRenderTargetDesc() { }
}
public struct VisualDrawRange
{
    public ulong IndexStart, IndexCount;
    public bool IsEmpty() => IndexCount == 0;
}
public struct VisualMeshDraw
{
    public VisualDrawRange Range;
    public Texture? Texture;
    public Shader? Shader;
}
public struct VisualTextDraw
{
    public VisualDrawRange Range;
    public TextServices? TextServices;
    public uint AtlasIndex = uint.MaxValue;
    public ETextPixelMode PixelMode = ETextPixelMode.Sdf;
    public VisualTextDraw() { }
}
public struct VisualAtlasDraw
{
    public VisualDrawRange Range;
    public TextServices? TextServices;
    public uint AtlasIndex = uint.MaxValue;
    public VisualAtlasDraw() { }
}
public enum EVisualClipKind : byte { Rect, Mesh }
public struct VisualClipDraw
{
    public EVisualClipKind Kind;
    public Rectf Bound;
    public VisualDrawRange Range;
}
public struct VisualBackdropDraw
{
    public VisualDrawRange Range;
    public Rectf Bound;
    public BatchCmdBackdrop? Command;
}
public abstract class VisualBackend : IDisposable
{
    public abstract bool BeginFrame();
    public abstract void UpdateVertexBuffer(ReadOnlySpan<MeshVertex> vertices);
    public abstract void UpdateIndexBuffer(ReadOnlySpan<uint> indices);
    public abstract void BeginRenderTarget(in VisualRenderTargetDesc desc);
    public abstract void BeginClip(in VisualClipDraw clip);
    public abstract void EndClip();
    public abstract void DrawMesh(in VisualMeshDraw draw);
    public abstract void DrawText(in VisualTextDraw draw);
    public abstract void DrawAtlas(in VisualAtlasDraw draw);
    public abstract void DrawBackdrop(in VisualBackdropDraw draw);
    public abstract void EndRenderTarget();
    public abstract bool EndFrame();
    public virtual void Dispose() { }
}
