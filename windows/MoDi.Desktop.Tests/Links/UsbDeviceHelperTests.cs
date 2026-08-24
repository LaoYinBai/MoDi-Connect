using MoDi.Desktop.Links;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class UsbDeviceHelperTests
{
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
    public void ResolveAdbExecutable_retains_path_fallback_for_developer_runs()
    {
        using var temp = TempDirectory.Create();

        var resolved = UsbDeviceHelper.ResolveAdbExecutable(temp.Path);

        Assert.Equal("adb", resolved);
    }
}
