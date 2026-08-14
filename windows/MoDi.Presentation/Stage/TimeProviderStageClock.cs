namespace MoDi.Presentation.Stage;

public sealed class TimeProviderStageClock : IStageClock
{
    private readonly TimeProvider _timeProvider;

    public TimeProviderStageClock(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
}
