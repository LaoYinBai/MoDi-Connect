using System.Threading.Channels;

namespace MoDi.Presentation.Stage;

public sealed class StageAnimationDirector
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);
    private readonly Channel<StageConnectionState> _requests;
    private readonly IStageClock _clock;
    private readonly object _requestGate = new();
    private StageConnectionState _lastRequested;
    private StageConnectionState _settledState = StageConnectionState.Disconnected;
    private bool _hasLastRequested;

    public StageAnimationDirector(IStageClock clock)
    {
        _clock = clock;
        _requests = Channel.CreateUnbounded<StageConnectionState>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    public event Action<StageConnectionState>? CeremonyStarted;
    public event Action<StageAnimationFrame>? FramePublished;

    public void Request(StageConnectionState target)
    {
        lock (_requestGate)
        {
            if (_hasLastRequested && _lastRequested == target)
                return;

            if (!_requests.Writer.TryWrite(target))
                throw new InvalidOperationException("The stage animation request queue is closed.");

            _lastRequested = target;
            _hasLastRequested = true;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var target in _requests.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            CeremonyStarted?.Invoke(target);
            await RunCeremonyAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    public static StageAnimationFrame FrameAt(StageConnectionState target, TimeSpan elapsed)
    {
        var milliseconds = Math.Max(0, elapsed.TotalMilliseconds);
        return target switch
        {
            StageConnectionState.Connected => ForwardFrame(milliseconds),
            StageConnectionState.Reconnecting => ReverseFrame(target, milliseconds),
            StageConnectionState.Disconnected => ReverseFrame(target, milliseconds),
            StageConnectionState.Handshaking => new StageAnimationFrame(
                target, 0, 0, false, 0, 0, false, false),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
    }

    public static StageAnimationFrame SettledFrame(StageConnectionState state) => state switch
    {
        StageConnectionState.Connected => FrameAt(state, TimeSpan.FromMilliseconds(2680)),
        StageConnectionState.Reconnecting or StageConnectionState.Disconnected =>
            FrameAt(state, TimeSpan.FromMilliseconds(2600)),
        _ => FrameAt(state, TimeSpan.Zero),
    };

    private async Task RunCeremonyAsync(
        StageConnectionState target,
        CancellationToken cancellationToken)
    {
        if (target == _settledState)
        {
            FramePublished?.Invoke(SettledFrame(target));
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var duration = target switch
        {
            StageConnectionState.Connected => TimeSpan.FromMilliseconds(2680),
            StageConnectionState.Reconnecting or StageConnectionState.Disconnected =>
                TimeSpan.FromMilliseconds(2600),
            _ => TimeSpan.Zero,
        };

        var elapsed = TimeSpan.Zero;
        FramePublished?.Invoke(FrameAt(target, elapsed));

        while (elapsed < duration)
        {
            var remaining = duration - elapsed;
            var step = remaining < FrameInterval ? remaining : FrameInterval;
            await _clock.DelayAsync(step, cancellationToken).ConfigureAwait(false);
            elapsed += step;
            FramePublished?.Invoke(FrameAt(target, elapsed));
        }

        _settledState = target;
    }

    private static StageAnimationFrame ForwardFrame(double milliseconds)
    {
        var boyTravel = Progress(milliseconds, 0, 1200);
        var colorReveal = Progress(milliseconds, 1280, 600);
        var waterLevel = Progress(milliseconds, 1880, 800);
        return new StageAnimationFrame(
            StageConnectionState.Connected,
            boyTravel,
            FrameIndex(boyTravel),
            false,
            colorReveal,
            waterLevel,
            milliseconds >= 1680,
            milliseconds >= 2680);
    }

    private static StageAnimationFrame ReverseFrame(
        StageConnectionState target,
        double milliseconds)
    {
        var returnProgress = Progress(milliseconds, 1400, 1200);
        return new StageAnimationFrame(
            target,
            1 - returnProgress,
            FrameIndex(returnProgress),
            milliseconds >= 1400,
            1 - Progress(milliseconds, 800, 600),
            1 - Progress(milliseconds, 0, 800),
            false,
            false);
    }

    private static int FrameIndex(double progress) =>
        Math.Min(15, (int)Math.Floor(Math.Clamp(progress, 0, 1) * 16));

    private static double Progress(double value, double start, double duration) =>
        Math.Clamp((value - start) / duration, 0, 1);
}
