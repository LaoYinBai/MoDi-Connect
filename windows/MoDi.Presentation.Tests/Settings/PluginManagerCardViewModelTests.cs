using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class PluginManagerCardViewModelTests
{
    [Fact]
    public void Built_in_audio_can_be_toggled_but_cannot_be_uninstalled()
    {
        var plugins = new RecordingPluginCatalogService();
        using var vm = new PluginManagerCardViewModel(plugins);
        var audio = Assert.Single(vm.Entries);

        Assert.True(audio.IsBuiltIn);
        Assert.True(audio.IsEnabled);
        Assert.False(audio.CanUninstall);
        Assert.True(vm.ToggleEnabledCommand.CanExecute(audio));
        Assert.False(vm.UninstallCommand.CanExecute(audio));

        vm.UninstallCommand.Execute(audio);
        Assert.Equal(0, plugins.UninstallCalls);
    }

    [Fact]
    public async Task Toggle_command_delegates_the_inverse_enabled_state()
    {
        var plugins = new RecordingPluginCatalogService();
        using var vm = new PluginManagerCardViewModel(plugins);
        var audio = Assert.Single(vm.Entries);

        await vm.ToggleEnabledCommand.ExecuteAsync(audio);

        Assert.Equal(1, plugins.SetEnabledCalls);
        Assert.Equal("built-in-audio", plugins.LastPluginId);
        Assert.False(plugins.LastEnabled);
    }
}
