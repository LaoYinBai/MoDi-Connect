using MoDi.App.Contracts;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Shell;

public sealed class TopBarViewModelTests
{
    [Fact]
    public async Task Theme_toggle_switches_between_the_two_finished_presets()
    {
        var appearance = new RecordingAppearanceService();
        using var viewModel = new TopBarViewModel(new NavigationViewModel(), appearance);

        Assert.True(viewModel.ShowSwitchToLightTheme);
        await viewModel.ToggleThemeCommand.ExecuteAsync();
        Assert.Equal(ThemePreset.PaperDay, appearance.Snapshot.Preset);
        Assert.True(viewModel.ShowSwitchToDarkTheme);

        await viewModel.ToggleThemeCommand.ExecuteAsync();
        Assert.Equal(ThemePreset.InkNight, appearance.Snapshot.Preset);
        Assert.True(viewModel.ShowSwitchToLightTheme);
    }
}
