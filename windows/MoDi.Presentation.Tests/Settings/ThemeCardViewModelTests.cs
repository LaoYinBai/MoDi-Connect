using MoDi.App.Contracts;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class ThemeCardViewModelTests
{
    [Fact]
    public void Theme_card_lists_only_two_presets_and_custom()
    {
        var appearance = new RecordingAppearanceService();
        using var vm = new ThemeCardViewModel(appearance);

        Assert.Equal(["墨·夜堤", "宣纸·昼堤", "自定义"], vm.Options.Select(option => option.DisplayName));
        Assert.True(Assert.Single(vm.Options, option => option.Preset == ThemePreset.InkNight).IsSelected);
    }

    [Fact]
    public async Task Selecting_a_theme_delegates_only_to_appearance()
    {
        var appearance = new RecordingAppearanceService();
        using var vm = new ThemeCardViewModel(appearance);
        var paper = Assert.Single(vm.Options, option => option.Preset == ThemePreset.PaperDay);

        await vm.SelectCommand.ExecuteAsync(paper);

        Assert.Equal(1, appearance.SelectPresetCalls);
        Assert.Equal(ThemePreset.PaperDay, appearance.LastPreset);
        Assert.Equal("宣纸·昼堤", vm.SelectedDisplayName);
    }
}
