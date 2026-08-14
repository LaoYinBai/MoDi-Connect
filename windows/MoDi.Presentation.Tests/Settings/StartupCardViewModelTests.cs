using MoDi.App.Contracts;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class StartupCardViewModelTests
{
    [Fact]
    public async Task Startup_failure_stays_on_the_startup_card()
    {
        var startup = new RecordingStartupService
        {
            SetEnabledResult = OperationResult.Failure("STARTUP_DENIED", "无法写入开机启动项")
        };
        using var vm = new StartupCardViewModel(startup);

        await vm.ToggleCommand.ExecuteAsync();

        Assert.Equal(1, startup.SetEnabledCalls);
        Assert.True(startup.LastEnabled);
        Assert.Equal("STARTUP_DENIED", vm.ErrorCode);
        Assert.Equal("无法写入开机启动项", vm.ErrorMessage);
    }

    [Fact]
    public async Task Unexpected_startup_exception_uses_a_stable_presentation_error()
    {
        var startup = new RecordingStartupService { SetEnabledException = new IOException("private detail") };
        using var vm = new StartupCardViewModel(startup);

        await vm.ToggleCommand.ExecuteAsync();

        Assert.Equal("PRESENTATION_STARTUP", vm.ErrorCode);
        Assert.DoesNotContain("private detail", vm.ErrorMessage);
    }
}
