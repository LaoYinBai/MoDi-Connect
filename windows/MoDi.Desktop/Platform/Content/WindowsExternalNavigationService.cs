using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Content;

internal interface IProcessLauncher
{
    void Open(string target);
}

internal sealed class ShellProcessLauncher : IProcessLauncher
{
    public void Open(string target) => Process.Start(new ProcessStartInfo(target)
    {
        UseShellExecute = true,
    });
}

public sealed class WindowsExternalNavigationService : IExternalNavigationService
{
    private readonly IReadOnlyDictionary<ExternalDestination, Uri> _destinations;
    private readonly IProcessLauncher _launcher;

    public WindowsExternalNavigationService(IReadOnlyDictionary<ExternalDestination, Uri> destinations)
        : this(destinations, new ShellProcessLauncher()) { }

    internal WindowsExternalNavigationService(
        IReadOnlyDictionary<ExternalDestination, Uri> destinations,
        IProcessLauncher launcher)
    {
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public Task<OperationResult> OpenAsync(
        ExternalDestination destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_destinations.TryGetValue(destination, out var uri))
            return Task.FromResult(OperationResult.Failure(
                "NAV_DESTINATION_UNAVAILABLE",
                "该外部入口尚未配置"));
        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(OperationResult.Failure(
                "NAV_SCHEME_REJECTED",
                "只允许打开经过配置的 HTTPS 地址"));

        try
        {
            _launcher.Open(uri.AbsoluteUri);
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Failure("NAV_OPEN", $"无法打开外部页面：{ex.Message}"));
        }
    }
}
