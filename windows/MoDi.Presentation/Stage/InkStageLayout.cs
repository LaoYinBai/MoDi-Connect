namespace MoDi.Presentation.Stage;

public static class InkStageLayout
{
    public const double StageHeight = 400;
    public const double BridgeScaleY = 0.70;
    public const double BridgeBaselineY = 296;
    public const double WalkFootOffsetY = 87;

    private const double GaussianSigma = 0.24;
    private const double ArchLift = 154;

    public static double WalkFootY(double travel)
    {
        var normalizedTravel = double.IsFinite(travel) ? Math.Clamp(travel, 0, 1) : 0;
        var edge = Gaussian(0);
        var normalizedArch = (Gaussian(normalizedTravel) - edge) / (1 - edge);
        return BridgeBaselineY - ArchLift * normalizedArch;
    }

    public static double WalkCanvasTop(double travel) => WalkFootY(travel) - WalkFootOffsetY;

    public static double FlattenBridgeY(double originalY) =>
        BridgeBaselineY + (originalY - BridgeBaselineY) * BridgeScaleY;

    private static double Gaussian(double travel)
    {
        var distance = (travel - 0.5) / GaussianSigma;
        return Math.Exp(-0.5 * distance * distance);
    }
}
