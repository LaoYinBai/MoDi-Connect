namespace MoDi.Presentation.Stage;

public enum InkBoyMode
{
    Idle,
    Walk,
    Stand,
}

public readonly record struct InkRevealOrigin(double X, double Y);

public sealed record InkStagePresentation(
    InkBoyMode BoyMode,
    double BoyCanvasLeft,
    double BoyCanvasTop,
    int BoyFrame,
    bool BoyMirrored,
    double ColorReveal,
    double WaterLevel,
    double EffectiveRms,
    InkRevealOrigin BridgeRevealOrigin,
    InkRevealOrigin BoyRevealOrigin,
    double RevealFeatherStart)
{
    private const double LeftBankCenter = 190;
    private const double RightBankCenter = 800;

    public static InkStagePresentation FromFrame(
        StageAnimationFrame frame,
        double elapsedSeconds,
        double rms)
    {
        var mode = ResolveBoyMode(frame);
        var frameIndex = mode == InkBoyMode.Idle ? IdleFrame(elapsedSeconds) : frame.BoyFrame;
        var left = mode switch
        {
            InkBoyMode.Walk => LeftBankCenter + (RightBankCenter - LeftBankCenter) * frame.BoyTravel - 125,
            InkBoyMode.Idle => LeftBankCenter - 250,
            _ => 0,
        };
        var top = mode switch
        {
            InkBoyMode.Walk => InkStageLayout.WalkCanvasTop(frame.BoyTravel),
            InkBoyMode.Idle => 134 + Math.Sin(elapsedSeconds * Math.PI * 2 / 3),
            _ => 0,
        };
        var normalizedRms = double.IsFinite(rms) ? Math.Clamp(rms, 0, 1) : 0;

        return new InkStagePresentation(
            mode,
            left,
            top,
            frameIndex,
            frame.BoyMirrored,
            frame.ColorReveal,
            frame.WaterLevel,
            frame.RmsEnabled ? normalizedRms : 0,
            new InkRevealOrigin(0.43, 0.56),
            new InkRevealOrigin(0.80, 0.64),
            0.82);
    }

    private static InkBoyMode ResolveBoyMode(StageAnimationFrame frame)
    {
        if (frame.State == StageConnectionState.Connected && frame.BoyTravel >= 1)
            return InkBoyMode.Stand;
        if (frame.State is StageConnectionState.Reconnecting or StageConnectionState.Disconnected &&
            frame.BoyTravel >= 1 && !frame.BoyMirrored)
            return InkBoyMode.Stand;
        if (frame.State == StageConnectionState.Handshaking || frame.BoyTravel <= 0)
            return InkBoyMode.Idle;
        return InkBoyMode.Walk;
    }

    private static int IdleFrame(double elapsedSeconds)
    {
        var normalized = ((elapsedSeconds % 6) + 6) % 6;
        return (int)(normalized / 1.5) switch
        {
            0 => 0,
            1 => 1,
            2 => 0,
            _ => 2,
        };
    }
}
