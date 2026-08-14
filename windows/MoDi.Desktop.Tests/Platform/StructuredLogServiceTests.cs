using MoDi.Desktop.Platform.Logging;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class StructuredLogServiceTests
{
    [Fact]
    public void Writer_rotates_on_utc_day_and_size_and_enforces_total_bound()
    {
        using var temp = TempDirectory.Create();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 11, 23, 59, 0, TimeSpan.Zero));
        using var writer = new StructuredLogService(temp.Path, clock, maxFileBytes: 240, maxTotalBytes: 720);

        for (var index = 0; index < 12; index++)
            writer.Write("INFO", "test", new string('x', 120) + index);
        clock.UtcNow = clock.UtcNow.AddDays(1);
        writer.Write("INFO", "test", "next day");

        var files = Directory.GetFiles(temp.Path, "*.jsonl");
        Assert.Contains(files, path => Path.GetFileName(path).Contains("20260812"));
        Assert.True(files.Length >= 2);
        Assert.True(files.Sum(path => new FileInfo(path).Length) <= 720);
    }

    [Fact]
    public void Core_adapter_never_leaks_sensitive_values_to_json_lines()
    {
        using var temp = TempDirectory.Create();
        using var writer = new StructuredLogService(temp.Path, TimeProvider.System);
        var adapter = new CoreLoggerAdapter(writer);

        adapter.Error("pair", "token=secret at 192.168.1.44", new InvalidOperationException(@"C:\Users\Alice\Music"));

        var content = File.ReadAllText(Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl")));
        Assert.DoesNotContain("secret", content);
        Assert.DoesNotContain("192.168.1.44", content);
        Assert.DoesNotContain("Alice", content);
        Assert.Contains("[REDACTED]", content);
        Assert.Contains("[IP]", content);
        Assert.Contains("[USER_PATH]", content);
    }
}
