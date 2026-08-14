namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
