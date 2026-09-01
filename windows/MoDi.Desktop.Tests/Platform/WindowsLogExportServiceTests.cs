using System.IO.Compression;
using MoDi.App.Contracts;
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
        var selectedPath = Path.Combine(temp.Path, "chosen", "support-bundle.zip");
        var saver = new RecordingLogArchiveSaveService(selectedPath);
        var service = new WindowsLogExportService(paths, new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 11, 2, 3, 4, TimeSpan.Zero)), saver);

        var result = await service.ExportAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.IncludedFileCount);
        Assert.Equal("support-bundle.zip", result.Value.ArchiveDisplayName);
        Assert.Equal("modi-logs-20260811-020304.zip", saver.SuggestedFileName);
        using var zip = ZipFile.OpenRead(selectedPath);
        var entry = Assert.Single(zip.Entries);
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.DoesNotContain("abc123", content);
        Assert.DoesNotContain("192.168.1.44", content);
        Assert.DoesNotContain("Alice", content);
        Assert.Empty(Directory.GetDirectories(paths.ExportsDirectory, ".staging-*"));
        Assert.Empty(Directory.GetFiles(paths.ExportsDirectory, "*.zip"));
    }

    [Fact]
    public async Task Export_failure_returns_a_result_instead_of_throwing_into_receiver_code()
    {
        using var temp = TempDirectory.Create();
        var blockedRoot = Path.Combine(temp.Path, "blocked");
        await File.WriteAllTextAsync(blockedRoot, "not a directory");
        var service = new WindowsLogExportService(
            new ApplicationDataPaths(blockedRoot),
            TimeProvider.System,
            new RecordingLogArchiveSaveService(Path.Combine(temp.Path, "chosen.zip")));

        var result = await service.ExportAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_EXPORT", result.ErrorCode);
    }

    [Fact]
    public async Task Cancelling_the_save_picker_leaves_no_archive_or_staging_directory()
    {
        using var temp = TempDirectory.Create();
        var paths = new ApplicationDataPaths(temp.Path);
        Directory.CreateDirectory(paths.LogsDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.LogsDirectory, "raw.jsonl"), "safe");
        var service = new WindowsLogExportService(paths, TimeProvider.System, new CancelledLogArchiveSaveService());

        var result = await service.ExportAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_EXPORT_CANCELLED", result.ErrorCode);
        Assert.Empty(Directory.GetFileSystemEntries(paths.ExportsDirectory));
    }

    private sealed class RecordingLogArchiveSaveService(string destinationPath) : ILogArchiveSaveService
    {
        public string? SuggestedFileName { get; private set; }

        public async Task<OperationResult<string>> SaveAsync(
            string suggestedFileName,
            string sourceArchivePath,
            CancellationToken cancellationToken)
        {
            SuggestedFileName = suggestedFileName;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var source = File.OpenRead(sourceArchivePath);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);
            return OperationResult<string>.Success(Path.GetFileName(destinationPath));
        }
    }

    private sealed class CancelledLogArchiveSaveService : ILogArchiveSaveService
    {
        public Task<OperationResult<string>> SaveAsync(
            string suggestedFileName,
            string sourceArchivePath,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<string>.Failure("LOG_EXPORT_CANCELLED", "已取消导出"));
    }
}
