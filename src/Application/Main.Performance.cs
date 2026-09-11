using System.Diagnostics;
namespace Earthward.Application;

public partial class Main
{
    private readonly System.Collections.Generic.List<Earthward.Domain.DataMap> _renderProjectiles = new();
    // Explicit opt-in for isolated pressure captures; normal play allocates no samples.
    public bool CaptureFrameTimings { get; set; }
    public double LastMainProcessMs { get; private set; }
    public double LastCombatStepMs { get; private set; }
    public double LastRenderSyncMs { get; private set; }
    public double LastHudDrawMs { get; private set; }
    private static double FrameElapsed(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;
}
