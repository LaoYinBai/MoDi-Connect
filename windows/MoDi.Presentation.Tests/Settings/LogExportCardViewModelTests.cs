using MoDi.App.Contracts;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class LogExportCardViewModelTests
{
    [Fact]
    public async Task Export_feedback_shows_only_the_archive_display_name()
    {
        var logs = new RecordingLogExportService
        {
            Result = OperationResult<LogExportReceipt>.Success(new LogExportReceipt("MoDi-test-logs.zip", 7))
        };
        using var vm = new LogExportCardViewModel(logs);

        await vm.ExportCommand.ExecuteAsync();

        Assert.Equal(1, logs.ExportCalls);
        Assert.Equal("MoDi-test-logs.zip", vm.ArchiveDisplayName);
        Assert.Equal("已导出：MoDi-test-logs.zip", vm.FeedbackText);
        Assert.DoesNotContain("\\", vm.FeedbackText);
        Assert.DoesNotContain("/", vm.FeedbackText);
    }

    [Fact]
    public async Task Cancelling_the_save_picker_is_feedback_not_an_error()
    {
        var logs = new RecordingLogExportService
        {
            Result = OperationResult<LogExportReceipt>.Failure("LOG_EXPORT_CANCELLED", "已取消导出")
        };
        using var vm = new LogExportCardViewModel(logs);

        await vm.ExportCommand.ExecuteAsync();

        Assert.Equal("已取消导出", vm.FeedbackText);
        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorCode);
    }
}
