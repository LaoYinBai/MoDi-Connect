using MoDi.Desktop.Links;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class UsbDeviceHelperTests
{
    [Theory]
    [InlineData("adb.exe: error: listener 'tcp:12348' not found")]
    [InlineData("ADB.EXE: ERROR: LISTENER 'TCP:12348' NOT FOUND")]
    public void Missing_forward_is_classified_as_idempotent_cleanup(string message)
    {
        Assert.True(UsbDeviceHelper.IsMissingForwardError(new IOException(message)));
    }

    [Fact]
    public void Unrelated_adb_failure_is_not_classified_as_missing_forward()
    {
        Assert.False(UsbDeviceHelper.IsMissingForwardError(new IOException("device offline")));
    }

    [Fact]
    public void ResolveAdbExecutable_prefers_the_application_private_platform_tools()
    {
        using var temp = TempDirectory.Create();
        var adb = Path.Combine(temp.Path, "tools", "adb", "adb.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(adb)!);
        File.WriteAllBytes(adb, [0x4D, 0x5A]);

        var resolved = UsbDeviceHelper.ResolveAdbExecutable(temp.Path);

        Assert.Equal(adb, resolved);
    }

    [Fact]
    public void Missing_private_adb_never_falls_back_to_host_path()
    {
        using var temp = TempDirectory.Create();

        Assert.Throws<FileNotFoundException>(() => UsbDeviceHelper.ResolveAdbExecutable(temp.Path));
    }

    [Fact]
    public async Task ReplaceForward_removes_only_the_owned_port_before_recreating_it()
    {
        var calls = new List<(string[] Arguments, bool StartIfNeeded)>();

        await UsbDeviceHelper.ReplaceForwardAsync(
            (arguments, _, startIfNeeded) =>
            {
                calls.Add((arguments, startIfNeeded));
                return Task.FromResult(string.Empty);
            },
            port: 12348,
            CancellationToken.None);

        Assert.Collection(
            calls,
            remove =>
            {
                Assert.Equal(new[] { "-d", "forward", "--remove", "tcp:12348" }, remove.Arguments);
                Assert.False(remove.StartIfNeeded);
            },
            create =>
            {
                Assert.Equal(new[] { "-d", "forward", "tcp:12348", "tcp:12348" }, create.Arguments);
                Assert.True(create.StartIfNeeded);
            });
    }

    [Fact]
    public async Task ReplaceForward_recreates_the_port_when_no_previous_forward_exists()
    {
        var calls = new List<string[]>();

        await UsbDeviceHelper.ReplaceForwardAsync(
            (arguments, _, _) =>
            {
                calls.Add(arguments);
                if (arguments.Contains("--remove"))
                    throw new IOException("listener 'tcp:12348' not found");
                return Task.FromResult(string.Empty);
            },
            port: 12348,
            CancellationToken.None);

        Assert.Equal(2, calls.Count);
        Assert.DoesNotContain("--no-rebind", calls[1]);
    }

    [Fact]
    public void Shutdown_lifetime_cancels_current_and_future_regular_adb_operations()
    {
        using var lifetime = new AdbOperationLifetime();
        using var beforeShutdown = lifetime.CreateLinkedSource(CancellationToken.None);
        Assert.False(beforeShutdown.IsCancellationRequested);

        lifetime.BeginShutdown();
        lifetime.BeginShutdown();

        Assert.True(beforeShutdown.IsCancellationRequested);
        using var afterShutdown = lifetime.CreateLinkedSource(CancellationToken.None);
        Assert.True(afterShutdown.IsCancellationRequested);
    }
}
