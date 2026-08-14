using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Shell;
using UITest.Demo;
using Xunit;

namespace UITest.Tests.Shell;

[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
public sealed class AvaloniaUiCollection;

[Collection("Avalonia UI")]
public sealed class MainWindowSmokeTests
{
    [Fact]
    public void Main_window_constructs_and_completes_its_first_measure()
    {
        EnsureAvaloniaApplication();
        var window = new MainWindow();
        try
        {
            window.Measure(new Size(1280, 720));
            Assert.IsType<TestUiComposition>(window.DataContext);
            Assert.Single(window.GetLogicalDescendants().OfType<AppShellView>());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Shared_content_host_follows_navigation_without_local_page_copies()
    {
        EnsureAvaloniaApplication();
        var window = new MainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var composition = Assert.IsType<TestUiComposition>(window.DataContext);
            var content = Assert.Single(window.GetLogicalDescendants().OfType<ContentControl>(),
                control => ReferenceEquals(control.Content, composition.Shell.CurrentPageViewModel));

            composition.Shell.Navigation.NavigateCommand.Execute(AppPage.Settings);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(composition.Shell.Settings, content.Content);
            Assert.Single(window.GetLogicalDescendants().OfType<SettingsPage>());
            Assert.False(Assert.Single(window.GetLogicalDescendants().OfType<FeatureRailView>()).IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Demo_state_flows_to_the_shared_status_bar()
    {
        EnsureAvaloniaApplication();
        var window = new MainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var composition = Assert.IsType<TestUiComposition>(window.DataContext);
            composition.Demo.SetReceiverStateCommand.Execute("connected");
            Dispatcher.UIThread.RunJobs();

            var status = Assert.Single(window.GetLogicalDescendants().OfType<StatusBarView>());
            Assert.Equal("已连接", status.FindControl<TextBlock>("ConnectionStatusText")?.Text);
            var indicator = Assert.IsType<Ellipse>(status.FindControl<Ellipse>("ConnectionStateIndicator"));
            Assert.Equal(Color.Parse("#47937F"), Assert.IsType<SolidColorBrush>(indicator.Fill).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public async Task Fake_appearance_changes_update_the_application_theme_variant()
    {
        EnsureAvaloniaApplication();
        var window = new MainWindow();
        try
        {
            var composition = Assert.IsType<TestUiComposition>(window.DataContext);
            await composition.Demo.ToggleThemeCommand.ExecuteAsync();
            Assert.Equal(ThemeVariant.Light, Application.Current?.RequestedThemeVariant);

            await composition.Demo.ToggleThemeCommand.ExecuteAsync();
            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);
        }
        finally
        {
            window.Close();
        }
    }

    private static void EnsureAvaloniaApplication()
    {
        if (Application.Current is null)
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
    }
}
