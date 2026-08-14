using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class SettingsCompositionTests
{
    [Fact]
    public void Settings_page_only_composes_children()
    {
        using var vm = PresentationTestFactory.CreateSettingsPage();

        Assert.NotNull(vm.Startup);
        Assert.NotNull(vm.Theme);
        Assert.NotNull(vm.CustomAppearance);
        Assert.NotNull(vm.NetworkStatus);
        Assert.NotNull(vm.PersonalizationReset);
        Assert.NotNull(vm.PluginManager);
        Assert.NotNull(vm.LogExport);
        Assert.DoesNotContain(typeof(SettingsPageViewModel).GetMethods(),
            method => method.DeclaringType == typeof(SettingsPageViewModel) && method.Name.EndsWith("Async"));
    }
}
