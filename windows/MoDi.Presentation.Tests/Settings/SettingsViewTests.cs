using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

[Collection("Avalonia UI")]
public sealed class SettingsViewTests
{
    [Fact]
    public void Settings_page_hides_custom_theme_editor_and_keeps_the_remaining_cards_in_order()
    {
        TestApplicationHost.Ensure();
        using var vm = PresentationTestFactory.CreateSettingsPage();
        var page = new SettingsPage { DataContext = vm };

        var cardTypes = page.GetLogicalDescendants()
            .Where(control => control is StartupCard or ThemeCard or CustomAppearanceCard or NetworkStatusCard
                or PersonalizationResetCard or PluginManagerCard or LogExportCard)
            .Select(control => control.GetType())
            .ToArray();

        Assert.Equal(
        [
            typeof(StartupCard), typeof(ThemeCard), typeof(NetworkStatusCard),
            typeof(PersonalizationResetCard), typeof(PluginManagerCard), typeof(LogExportCard)
        ], cardTypes);
        Assert.Empty(page.GetLogicalDescendants().OfType<CustomAppearanceCard>());
    }

    [Fact]
    public void Card_actions_are_command_bound_and_share_the_required_ui_font()
    {
        var application = TestApplicationHost.Ensure();
        using var vm = PresentationTestFactory.CreateSettingsPage();
        var page = new SettingsPage { DataContext = vm };
        var window = new Window { Width = 900, Height = 760, Content = page };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var startup = Assert.Single(page.GetLogicalDescendants().OfType<StartupCard>());
            var reset = Assert.Single(page.GetLogicalDescendants().OfType<PersonalizationResetCard>());
            var logs = Assert.Single(page.GetLogicalDescendants().OfType<LogExportCard>());
            Assert.Same(vm.Startup.ToggleCommand, startup.FindControl<CheckBox>("StartupToggle")?.Command);
            Assert.Same(vm.PersonalizationReset.ConfirmResetCommand, reset.FindControl<Button>("ResetPersonalizationButton")?.Command);
            Assert.Same(vm.LogExport.ExportCommand, logs.FindControl<Button>("ExportLogsButton")?.Command);

            Assert.True(application.TryFindResource("FontFamilyDefault", out var expected));
            foreach (var card in page.GetLogicalDescendants().OfType<UserControl>())
                Assert.Equal(Assert.IsType<FontFamily>(expected), card.GetValue(TextElement.FontFamilyProperty));
        }
        finally
        {
            window.Close();
        }
    }
}
