using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakePairingService : IPairingService
{
    private static readonly byte[] DemoQrPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAagAAAGoAQAAAAA7yUxtAAACrklEQVR4nO1Zy47bQAzz//90is2ID2W7BdpDOQcm2CS2h7MAZVGU/Lz+5fUUVTZ6bzRTqhvVw3x1eM7r6+Dr8znfr/eJc/g+mFVFxdlA6A7wvfDsMufmxwYUlWNjYnzifNafdfit3Yq6h43H11IyCSzqLjZOMnKDUcwjqkXdxMZgpwJCMwfppa6oG9igS/nj+0dvU9T/ZcPtPQzKibIi/b0LKCrGBsO8DT/ylAn7kcZFZdhYyslP2wRlb64WFWcDQVR7hmYNUVdhLCrOxuQmW+2Toey02bTtWllUho2RSnmVGWNxPGKhXbWyqAgbcvtcZJNHJa6uFpVkw5SU4aWKmr3E2qKybCjxOMbS8bd+rqg0G7qKeeOqeNpIRrOoIBvIPhh9hnVJrOVlUWE26EhgWT6mVpLSovJsIMhW+aikjO7sur1lUQk2IKIqcFJVFjpWxKLSbKCaUS81YWTIrf4VFWYD+bYnjewBxrtomFVUlo3xJ/CQFlWbkvAuKCrOxn7uYsqqnJ2rq3coKsOGJBM+hV3atAR0/0VdwIZ5Sv4p+LgHsH1RaTYstOinzXDCrSjIRWXZkG7CqFhUlZRK0KKybGxx5BawkqOw9g+KSrJh02B4yimAHEXOmZXLRYXYQDttAsqpyErFoi5hQzZEw2INhT/MZ1E3sDHpuR6b6XsGI7vXKyrEhplKLDUTadm6ZylFhdhwTwL3qEeeKH9K06KibDC+1gjQkhCzBbaoHBvMTBhLSCjHImehHqEVlWSDE2IOs7gCWTvHe5ZSVIgN1D2bXynKZlmWSykqxwbUcySV1Y5d9u+ewhSVZgNJh4Db/JGpWdQ9bEyM0Z+tZ2coe0VdwAZVVCfGv0ysR1VxsqgoG5oxcsCP8GJibFEvKszGX7+KKhu9N5op1Y3q4StaHX4BPAuzUoqCzFAAAAAASUVORK5CYII=");

    private readonly TimeProvider _timeProvider;

    public FakePairingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        Snapshot = CreateSnapshot();
    }

    public PairingSnapshot Snapshot { get; private set; }
    public OperationResult RefreshResult { get; set; } = OperationResult.Success();
    public OperationResult ConnectResult { get; set; } = OperationResult.Success();
    public int RefreshCalls { get; private set; }
    public int ConnectCalls { get; private set; }
    public string? LastConnectedDeviceId { get; private set; }
    public event Action<PairingSnapshot>? SnapshotChanged;

    public Task<OperationResult> RefreshQrAsync(CancellationToken cancellationToken)
    {
        RefreshCalls++;
        if (RefreshResult.IsSuccess)
            Publish(CreateSnapshot());
        return Task.FromResult(RefreshResult);
    }

    public Task<OperationResult> ConnectAsync(string deviceId, CancellationToken cancellationToken)
    {
        ConnectCalls++;
        LastConnectedDeviceId = deviceId;
        return Task.FromResult(ConnectResult);
    }

    public void Publish(PairingSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }

    private PairingSnapshot CreateSnapshot() => new(
        DemoQrPng,
        "LAN Audio Bridge",
        _timeProvider.GetUtcNow().AddMinutes(2),
        [new PairedDeviceSnapshot("recent-p2p", "工作室 Mac", "上次连接：今天")],
        IsRefreshing: false,
        ErrorCode: null,
        ErrorMessage: null);
}
