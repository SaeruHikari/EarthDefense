using Earthward.Domain;

namespace Earthward.Presentation;

/// <summary>A view of the director's committed relocation, never a prediction of combat victory.</summary>
public sealed record FrontierWarning(string TransitionId, int NextStage, double Radius, double Altitude, double Remaining, double WindowDuration)
{
    public static FrontierWarning? FromStatus(DataMap status, double earthRadius)
    {
        if (!status.B("enabled") || status.Value("frontier_warning") is not DataMap warning || !warning.B("pending"))
            return null;
        int stage = warning.I("next_stage");
        double remaining = warning.N("remaining"), duration = warning.N("window_duration"), radius = warning.N("target_radius");
        string id = warning.S("transition_id");
        if (stage is < 1 or > 3 || id == "" || !double.IsFinite(remaining) || !double.IsFinite(duration) || !double.IsFinite(radius)
            || !double.IsFinite(earthRadius) || earthRadius <= 0 || radius <= earthRadius || duration <= 0 || remaining <= 0 || remaining > duration + .000001)
            return null;
        return new FrontierWarning(id, stage, radius, radius - earthRadius, remaining, duration);
    }
}
