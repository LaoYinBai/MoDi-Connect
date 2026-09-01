using System.Diagnostics;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;
using MoDi.Desktop.Platform.Runtime;
using MoDi.Desktop.Links;
#if MODI_OFFICIAL_GITEE
using MoDi.Desktop.Platform.Updates.Official;
#endif

namespace MoDi.Desktop.Tests.Platform;

public sealed class PrivateEnvironmentTests
{
    [Fact]
    public void Child_environment_discards_external_tools_and_redirects_writable_state()
    {
        using var temp = TempDirectory.Create();
        var start = new ProcessStartInfo();
        start.Environment["ADB_SERVER_SOCKET"] = "tcp:host:5037";
        start.Environment["ANDROID_SERIAL"] = "other-device";
        start.Environment["GIT_CONFIG_COUNT"] = "1";
        start.Environment["DOTNET_STARTUP_HOOKS"] = "host-hook.dll";
        PrivateToolEnvironment.Apply(start, temp.Path, @"C:\app\tools");
        Assert.False(start.Environment.ContainsKey("ADB_SERVER_SOCKET"));
        Assert.False(start.Environment.ContainsKey("ANDROID_SERIAL"));
        Assert.False(start.Environment.ContainsKey("GIT_CONFIG_COUNT"));
        Assert.False(start.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
        Assert.Equal(Path.Combine(temp.Path, "home"), start.Environment["USERPROFILE"]);
        Assert.Equal(Path.Combine(temp.Path, "home", ".android"), start.Environment["ANDROID_USER_HOME"]);
        Assert.Equal(@"C:\app\tools;" + Environment.GetFolderPath(Environment.SpecialFolder.System), start.Environment["PATH"]);
    }

    [Fact]
    public void Adb_commands_target_private_endpoint_and_require_bundled_dlls()
    {
        using var temp = TempDirectory.Create();
        var bin = Path.Combine(temp.Path, "tools", "adb");
        Directory.CreateDirectory(bin);
        foreach (var name in new[] { "adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll" }) File.WriteAllText(Path.Combine(bin, name), "fixture");
        var start = PrivateAdbRuntime.CreateStartInfo(temp.Path, Path.Combine(temp.Path, "state"), 25000, ["devices"]);
        Assert.Equal(new[] { "-L", "tcp:25000", "devices" }, start.ArgumentList);
        Assert.Equal(Path.Combine(bin, "adb.exe"), start.FileName);
        Assert.Equal(
            Path.Combine(temp.Path, "state", "auth", "adbkey"),
            start.Environment["ADB_VENDOR_KEYS"]);
        Assert.DoesNotContain(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android"),
            start.Environment["ADB_VENDOR_KEYS"],
            StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => PrivateAdbRuntime.CreateStartInfo(temp.Path, temp.Path, 5037, ["devices"]));
        File.Delete(Path.Combine(bin, "AdbWinApi.dll"));
        Assert.Throws<FileNotFoundException>(() => PrivateAdbRuntime.CreateStartInfo(temp.Path, temp.Path, 25000, ["devices"]));
    }

    [Fact]
    public async Task Disposing_job_terminates_its_owned_server()
    {
        using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            { Arguments = "-NoProfile -Command Start-Sleep -Seconds 30", UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            using (var job = new OwnedProcessJob()) { job.Assign(process); Assert.False(process.HasExited); }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
#if MODI_OFFICIAL_GITEE
    [Fact]
    public async Task Git_child_does_not_inherit_host_profile_or_search_path()
    {
        using var temp = TempDirectory.Create();
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var result = await new GitProcessRunner().RunAsync(executable,
            ["/d", "/c", "echo %USERPROFILE%&echo %PATH%&echo %GIT_CONFIG_GLOBAL%"], temp.Path,
            new Dictionary<string, string?>(), [], null, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEqual(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), result.StandardOutput.Split('\n')[0].Trim());
        Assert.Contains("NUL", result.StandardOutput);
        Assert.DoesNotContain("Android Studio", result.StandardOutput);
    }
#endif
}
