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
    public void Missing_private_adb_never_falls_back_to_host_path()
    {
        using var temp = TempDirectory.Create();

        Assert.Throws<FileNotFoundException>(() => UsbDeviceHelper.ResolveAdbExecutable(temp.Path));
    }
}
