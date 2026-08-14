namespace MoDi.Presentation.Stage;

public readonly record struct InkRevealState(double Opacity, double Radius, bool UsesMask);

public static class InkRevealModel
{
    private const double CompletedThreshold = 0.999;

    public static InkRevealState FromProgress(double reveal)
    {
        var progress = double.IsFinite(reveal) ? Math.Clamp(reveal, 0, 1) : 0;
        return new InkRevealState(
            progress <= 0 ? 0 : 1,
            Math.Max(0.001, progress * 1.35),
            progress < CompletedThreshold);
    }
}
