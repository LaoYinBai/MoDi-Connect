namespace MoDi.Presentation.Stage;

public sealed record StageAnimationFrame(
    StageConnectionState State,
    double BoyTravel,
    int BoyFrame,
    bool BoyMirrored,
    double ColorReveal,
    double WaterLevel,
    bool StatusGreen,
    bool RmsEnabled);
