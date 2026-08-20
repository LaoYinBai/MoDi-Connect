namespace MoDi.App.Contracts;

public sealed record DiagnosticResult(string Key, bool IsSuccess, string Message);

public sealed record OnboardingSnapshot(
    bool IsCompleted,
    int CurrentStep,
    IReadOnlyList<DiagnosticResult> Diagnostics)
{
    public static OnboardingSnapshot Default { get; } = new(false, 0, []);
}
