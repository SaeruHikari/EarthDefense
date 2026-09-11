using Godot;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    public bool CaptureFrameTimings { get; private set; }
    public double LastPlanetProcessMs { get; private set; }
    public double LastDroneSyncMs { get; private set; }
    public double LastEnemySyncMs { get; private set; }
    public double LastProjectileSyncMs { get; private set; }
    public double LastEffectsSyncMs { get; private set; }
    public void MeasureRenderTime(bool enabled) { CaptureFrameTimings = enabled; RenderingServer.ViewportSetMeasureRenderTime(_viewport.GetViewportRid(), enabled); }
    public double LastViewportGpuMs => RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport.GetViewportRid());
    public double LastViewportCpuMs => RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport.GetViewportRid());
    public int RenderedFleetBatchCount => _fleet.BatchCount;
    public int RenderedFleetPartCount => _fleet.PrototypePartCount;
}
