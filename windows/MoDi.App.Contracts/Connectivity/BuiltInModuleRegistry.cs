namespace MoDi.App.Contracts.Connectivity;

public enum BuiltInModuleState { Unavailable, Ready, Failed, Closed }

public sealed record BuiltInModuleDescriptor(
    string Id,
    ConnectivityCapability Capability,
    ChannelDescriptor Channel);

public sealed record BuiltInModuleSnapshot(
    SessionId SessionId,
    string ModuleId,
    ConnectivityCapability Capability,
    BuiltInModuleState State,
    string? FailureReason = null);

public readonly record struct BuiltInModuleResult(
    bool Succeeded,
    BuiltInModuleState State,
    string? FailureReason = null);

public static class BuiltInModules
{
    public static readonly BuiltInModuleDescriptor Audio = new(
        "audio", ConnectivityCapability.Audio, AudioChannel.Descriptor);
    public static readonly BuiltInModuleDescriptor Clipboard = new(
        "clipboard", ConnectivityCapability.Clipboard, ClipboardChannel.Descriptor);

    public static IBuiltInModule CreateAudio(
        Func<SessionId, ReadOnlyMemory<byte>, CancellationToken, Task> handle,
        Func<SessionId, IChannelDataPlane, CancellationToken, Task>? start = null,
        Func<SessionId, CancellationToken, Task>? stop = null) =>
        new ChannelBoundBuiltInModule(Audio, handle, start, stop);

    public static IBuiltInModule CreateClipboard(
        Func<SessionId, ReadOnlyMemory<byte>, CancellationToken, Task> handle,
        Func<SessionId, IChannelDataPlane, CancellationToken, Task>? start = null,
        Func<SessionId, CancellationToken, Task>? stop = null) =>
        new ChannelBoundBuiltInModule(Clipboard, handle, start, stop);

    private sealed class ChannelBoundBuiltInModule(
        BuiltInModuleDescriptor descriptor,
        Func<SessionId, ReadOnlyMemory<byte>, CancellationToken, Task> handle,
        Func<SessionId, IChannelDataPlane, CancellationToken, Task>? start,
        Func<SessionId, CancellationToken, Task>? stop) : IBuiltInModule
    {
        private readonly Func<SessionId, ReadOnlyMemory<byte>, CancellationToken, Task> _handle =
            handle ?? throw new ArgumentNullException(nameof(handle));
        public BuiltInModuleDescriptor Descriptor { get; } = descriptor;
        public Task StartAsync(SessionId sessionId, IChannelDataPlane channel, CancellationToken cancellationToken) =>
            start?.Invoke(sessionId, channel, cancellationToken) ?? Task.CompletedTask;
        public Task HandleAsync(SessionId sessionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            _handle(sessionId, payload, cancellationToken);
        public Task StopAsync(SessionId sessionId, CancellationToken cancellationToken) =>
            stop?.Invoke(sessionId, cancellationToken) ?? Task.CompletedTask;
    }
}

public interface IBuiltInModule
{
    BuiltInModuleDescriptor Descriptor { get; }
    Task StartAsync(SessionId sessionId, IChannelDataPlane channel, CancellationToken cancellationToken);
    Task HandleAsync(SessionId sessionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    Task StopAsync(SessionId sessionId, CancellationToken cancellationToken);
}

/// <summary>
/// Registry for compile-time built-in modules only. It never loads assemblies, scripts or remote code.
/// Module failures are converted to per-module state and never mutate the owning Session.
/// </summary>
public sealed class BuiltInModuleRegistry
{
    private sealed record ActiveModule(IBuiltInModule Module, IChannelDataPlane Channel);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IReadOnlyDictionary<string, IBuiltInModule> _modules;
    private readonly Dictionary<(SessionId Session, string Module), ActiveModule> _active = [];
    private readonly Dictionary<(SessionId Session, string Module), BuiltInModuleSnapshot> _snapshots = [];

    public BuiltInModuleRegistry(IEnumerable<IBuiltInModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var materialized = modules.ToArray();
        if (materialized.Any(module => string.IsNullOrWhiteSpace(module.Descriptor.Id)))
            throw new ArgumentException("Built-in module IDs must be non-empty.", nameof(modules));
        if (materialized.Select(module => module.Descriptor.Id).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ArgumentException("Built-in module IDs must be unique.", nameof(modules));
        _modules = materialized.ToDictionary(module => module.Descriptor.Id, StringComparer.Ordinal);
        RegisteredCapabilities = materialized.Select(module => module.Descriptor.Capability).Distinct().Order().ToArray();
    }

    public IReadOnlyList<ConnectivityCapability> RegisteredCapabilities { get; }

    public IReadOnlyList<BuiltInModuleSnapshot> Snapshots
    {
        get
        {
            _gate.Wait();
            try
            {
                return _snapshots.Values
                    .OrderBy(snapshot => snapshot.SessionId.Value, StringComparer.Ordinal)
                    .ThenBy(snapshot => snapshot.ModuleId, StringComparer.Ordinal)
                    .ToArray();
            }
            finally { _gate.Release(); }
        }
    }

    public async Task<BuiltInModuleResult> ActivateAsync(
        SessionId sessionId,
        string moduleId,
        IChannelDataPlane channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!_modules.TryGetValue(moduleId, out var module))
            return new(false, BuiltInModuleState.Unavailable, "Built-in module is not registered.");
        if (channel.SessionId != sessionId || channel.Descriptor != module.Descriptor.Channel)
            return new(false, BuiltInModuleState.Unavailable, "Channel does not match the built-in module.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = (sessionId, moduleId);
            if (_active.ContainsKey(key))
                return new(false, _snapshots[key].State, "Built-in module is already active for this session.");

            try
            {
                await channel.OpenAsync(cancellationToken).ConfigureAwait(false);
                await module.StartAsync(sessionId, channel, cancellationToken).ConfigureAwait(false);
                _active.Add(key, new(module, channel));
                SetSnapshot(sessionId, module.Descriptor, BuiltInModuleState.Ready);
                return new(true, BuiltInModuleState.Ready);
            }
            catch (OperationCanceledException)
            {
                await CloseIgnoringFailureAsync(channel, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                await CloseIgnoringFailureAsync(channel, CancellationToken.None).ConfigureAwait(false);
                SetSnapshot(sessionId, module.Descriptor, BuiltInModuleState.Failed, error.GetType().Name);
                return new(false, BuiltInModuleState.Failed, error.GetType().Name);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<BuiltInModuleResult> DispatchAsync(
        SessionId sessionId,
        string moduleId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = (sessionId, moduleId);
            if (!_active.TryGetValue(key, out var active))
                return new(false, BuiltInModuleState.Unavailable, "Built-in module is not active.");
            if (_snapshots[key].State != BuiltInModuleState.Ready)
                return new(false, _snapshots[key].State, _snapshots[key].FailureReason);

            try
            {
                await active.Module.HandleAsync(sessionId, payload, cancellationToken).ConfigureAwait(false);
                return new(true, BuiltInModuleState.Ready);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                SetSnapshot(sessionId, active.Module.Descriptor, BuiltInModuleState.Failed, error.GetType().Name);
                return new(false, BuiltInModuleState.Failed, error.GetType().Name);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task StopSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var pair in _active.Where(pair => pair.Key.Session == sessionId).ToArray())
            {
                try { await pair.Value.Module.StopAsync(sessionId, cancellationToken).ConfigureAwait(false); }
                catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
                await CloseIgnoringFailureAsync(pair.Value.Channel, cancellationToken).ConfigureAwait(false);
                SetSnapshot(sessionId, pair.Value.Module.Descriptor, BuiltInModuleState.Closed);
                _active.Remove(pair.Key);
            }
        }
        finally { _gate.Release(); }
    }

    private void SetSnapshot(
        SessionId sessionId,
        BuiltInModuleDescriptor descriptor,
        BuiltInModuleState state,
        string? failureReason = null) =>
        _snapshots[(sessionId, descriptor.Id)] = new(sessionId, descriptor.Id, descriptor.Capability, state, failureReason);

    private static async Task CloseIgnoringFailureAsync(IChannelDataPlane channel, CancellationToken cancellationToken)
    {
        try { await channel.CloseAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }
}
