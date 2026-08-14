using MoDi.Desktop.Platform.Startup;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class WindowsStartupServiceTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [Fact]
    public async Task Enable_writes_only_the_current_user_run_value_with_quoted_path()
    {
        var registry = new MemoryRegistryStore();
        var service = new WindowsStartupService(registry, @"C:\Program Files\MoDi\MoDi.exe");

        var result = await service.SetEnabledAsync(true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, registry.WriteCalls);
        Assert.Equal(RunKey, registry.LastSubKey);
        Assert.Equal("MoDi", registry.LastValueName);
        Assert.Equal("\"C:\\Program Files\\MoDi\\MoDi.exe\" --background", registry.LastWrittenValue);
        Assert.True(service.Snapshot.IsEnabled);
        Assert.True(service.Snapshot.IsAvailable);
    }

    [Fact]
    public async Task Disable_deletes_only_the_current_user_run_value()
    {
        var registry = new MemoryRegistryStore { Value = "existing" };
        var service = new WindowsStartupService(registry, @"C:\MoDi\MoDi.exe");

        var result = await service.SetEnabledAsync(false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, registry.DeleteCalls);
        Assert.Equal(RunKey, registry.LastSubKey);
        Assert.Equal("MoDi", registry.LastValueName);
        Assert.False(service.Snapshot.IsEnabled);
    }

    [Fact]
    public async Task Registry_failure_is_published_only_on_startup_snapshot()
    {
        var registry = new MemoryRegistryStore();
        var service = new WindowsStartupService(registry, @"C:\MoDi\MoDi.exe");
        registry.Exception = new UnauthorizedAccessException("denied");

        var result = await service.SetEnabledAsync(true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("STARTUP_REGISTRY_ACCESS", result.ErrorCode);
        Assert.False(service.Snapshot.IsAvailable);
        Assert.Equal("STARTUP_REGISTRY_ACCESS", service.Snapshot.ErrorCode);
    }
}
