using System.IO.Compression;
using MoDi.Desktop.Platform.Logging;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class WindowsLogExportServiceTests
{
    [Fact]
    public async Task Zip_contains_only_second_pass_redacted_copies_and_no_staging_directory()
    {
        using var temp = TempDirectory.Create();
        var paths = new ApplicationDataPaths(temp.Path);
        Directory.CreateDirectory(paths.LogsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(paths.LogsDirectory, "raw.jsonl"),
            "token=abc123 192.168.1.44 " + @"C:\Users\Alice\Music");
        var service = new WindowsLogExportService(paths, new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 11, 2, 3, 4, TimeSpan.Zero)));

        var result = await service.ExportAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.IncludedFileCount);
        Assert.Equal(Path.GetFileName(result.Value.ArchiveDisplayName), result.Value.ArchiveDisplayName);
        var zipPath = Path.Combine(paths.ExportsDirectory, result.Value.ArchiveDisplayName);
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = Assert.Single(zip.Entries);
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.DoesNotContain("abc123", content);
        Assert.DoesNotContain("192.168.1.44", content);
        Assert.DoesNotContain("Alice", content);
        Assert.Empty(Directory.GetDirectories(paths.ExportsDirectory, ".staging-*"));
    }

    [Fact]
    public async Task Export_failure_returns_a_result_instead_of_throwing_into_receiver_code()
    {
        using var temp = TempDirectory.Create();
        var blockedRoot = Path.Combine(temp.Path, "blocked");
        await File.WriteAllTextAsync(blockedRoot, "not a directory");
        var service = new WindowsLogExportService(new ApplicationDataPaths(blockedRoot), TimeProvider.System);

        var result = await service.ExportAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_EXPORT", result.ErrorCode);
    }
}
