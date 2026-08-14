namespace MoDi.Presentation.Stage;

public interface IStageClock
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
