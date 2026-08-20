using MoDi.App.Contracts;
using MoDi.Presentation.Onboarding;
using Xunit;

namespace MoDi.Presentation.Tests.Onboarding;

public sealed class OnboardingViewModelTests
{
    [Fact]
    public async Task Navigation_never_exceeds_four_steps_and_skip_remains_available()
    {
        var service = new RecordingOnboardingService();
        using var viewModel = new OnboardingViewModel(service);
        viewModel.ShowIfIncomplete();

        for (var index = 0; index < 8; index++)
            viewModel.NextCommand.Execute(null);

        Assert.Equal(3, viewModel.CurrentStep);
        Assert.True(viewModel.IsVisible);
        await viewModel.SkipCommand.ExecuteAsync();
        Assert.False(viewModel.IsVisible);
        Assert.Equal(1, service.SkipCalls);
    }

    private sealed class RecordingOnboardingService : IOnboardingService
    {
        public OnboardingSnapshot Snapshot { get; private set; } = OnboardingSnapshot.Default;
        public event Action<OnboardingSnapshot>? SnapshotChanged;
        public int SkipCalls { get; private set; }
        public Task<OperationResult> RunDiagnosticsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());
        public Task<OperationResult> CompleteAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());
        public Task<OperationResult> SkipAsync(CancellationToken cancellationToken)
        {
            SkipCalls++;
            Snapshot = Snapshot with { IsCompleted = true };
            SnapshotChanged?.Invoke(Snapshot);
            return Task.FromResult(OperationResult.Success());
        }
    }
}
