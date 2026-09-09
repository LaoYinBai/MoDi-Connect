using MoDi.Desktop.Connectivity.Receiver;
using MoDi.Desktop.Services;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class ReceiverLifecycleCoordinatorTests
{
    [Fact]
    public async Task Repeated_initialization_starts_each_listener_once()
    {
        var links = new RecordingReceiverLinkRuntime();
        using var coordinator = new ReceiverLifecycleCoordinator(links);

        var first = await coordinator.InitializeAsync();
        var second = await coordinator.InitializeAsync();

        Assert.Empty(first.Links.Failed);
        Assert.Empty(second.Links.Failed);
        Assert.Equal(1, links.LanStarts);
        Assert.Equal(1, links.BluetoothStarts);
        Assert.Equal(1, links.UsbStarts);
        Assert.Equal(1, links.P2pStarts);
    }

    [Fact]
    public async Task Restarting_p2p_stops_before_starting_and_dispose_is_idempotent()
    {
        var links = new RecordingReceiverLinkRuntime();
        var coordinator = new ReceiverLifecycleCoordinator(links);
        await coordinator.InitializeAsync();

        await coordinator.RestartP2pAsync();
        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Equal(["p2p:start", "p2p:stop", "p2p:start", "dispose"], links.P2pLifecycle);
        Assert.Equal(1, links.DisposeCalls);
    }

    [Fact]
    public async Task Candidate_discovery_never_connects_without_explicit_target_confirmation()
    {
        var links = new RecordingReceiverLinkRuntime();
        using var coordinator = new ReceiverLifecycleCoordinator(links);
        await coordinator.InitializeAsync();

        links.ReportCandidate(new P2pCandidateInfo("candidate", "phone"));

        Assert.Equal(0, links.ExplicitConnectCalls);
        Assert.True(coordinator.ConnectP2pCandidate("candidate"));
        Assert.Equal(1, links.ExplicitConnectCalls);
    }

    [Fact]
    public void Controller_projects_runtime_events_and_unsubscribes_before_shutdown()
    {
        var links = new RecordingReceiverLinkRuntime();
        var controller = new ReceiverController(links);
        var changes = 0;
        string? qr = null;
        controller.SnapshotChanged += () => changes++;
        controller.QrPayloadChanged += (payload, _) => qr = payload;

        links.ReportState(ConnectionState.Connected);
        links.ReportActiveLink("lan");
        links.ReportRoute(2);
        links.ReportStatus("lan", "已连接");
        links.ReportProgressVisible(true);
        links.ReportProgress(false, 0.5);
        links.ReportQr("payload", "device");
        links.ReportCandidate(new P2pCandidateInfo("candidate", "phone"));

        Assert.Equal(ConnectionState.Connected, controller.ConnectionState);
        Assert.Equal("lan", controller.ActiveLink);
        Assert.Equal(2, controller.CurrentRoute);
        Assert.Equal("已连接", controller.LanStatus);
        Assert.True(controller.IsP2pProgressVisible);
        Assert.False(controller.IsP2pProgressIndeterminate);
        Assert.Equal(0.5, controller.P2pProgress);
        Assert.Equal("payload", qr);
        Assert.Single(controller.P2pCandidates);
        Assert.True(changes >= 7);

        controller.Dispose();
        var atShutdown = changes;
        links.ReportState(ConnectionState.Error);
        Assert.Equal(atShutdown, changes);
    }

    private sealed class RecordingReceiverLinkRuntime : IReceiverLinkRuntime
    {
        public event Action<ConnectionState>? ConnectionStateChanged;
        public event Action<string>? ActiveLinkChanged;
        public event Action<int>? RouteChanged;
        public event Action<string, string>? LinkStatusChanged;
        public event Action<bool>? P2pProgressVisibleChanged;
        public event Action<bool, double>? P2pProgressChanged;
        public event Action<string?, string?>? QrChanged;
        public event Action<IReadOnlyList<P2pCandidateInfo>>? P2pCandidatesChanged;

        public int LanStarts { get; private set; }
        public int P2pStarts { get; private set; }
        public int BluetoothStarts { get; private set; }
        public int UsbStarts { get; private set; }
        public int ExplicitConnectCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public List<string> P2pLifecycle { get; } = [];
        public double Volume { get; set; } = 1;

        public Task<bool> StartLanAsync() => Task.FromResult(++LanStarts > 0);
        public Task<bool> StartP2pAsync()
        {
            P2pStarts++;
            P2pLifecycle.Add("p2p:start");
            return Task.FromResult(true);
        }
        public Task StopP2pAsync()
        {
            P2pLifecycle.Add("p2p:stop");
            return Task.CompletedTask;
        }
        public Task<bool> StartBluetoothAsync() => Task.FromResult(++BluetoothStarts > 0);
        public Task<bool> StartUsbAsync() => Task.FromResult(++UsbStarts > 0);
        public bool ConnectP2pCandidate(string deviceId)
        {
            ExplicitConnectCalls++;
            return deviceId == "candidate";
        }
        public void ReportState(ConnectionState state) => ConnectionStateChanged?.Invoke(state);
        public void ReportActiveLink(string link) => ActiveLinkChanged?.Invoke(link);
        public void ReportRoute(int route) => RouteChanged?.Invoke(route);
        public void ReportStatus(string link, string message) => LinkStatusChanged?.Invoke(link, message);
        public void ReportProgressVisible(bool visible) => P2pProgressVisibleChanged?.Invoke(visible);
        public void ReportProgress(bool indeterminate, double value) => P2pProgressChanged?.Invoke(indeterminate, value);
        public void ReportQr(string? payload, string? deviceName) => QrChanged?.Invoke(payload, deviceName);
        public void ReportCandidate(P2pCandidateInfo candidate) => P2pCandidatesChanged?.Invoke([candidate]);
        public void Dispose()
        {
            DisposeCalls++;
            P2pLifecycle.Add("dispose");
        }
    }
}
