using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Shell;

[Collection("Avalonia UI")]
public sealed class ShellGeometryTests
{
    [Fact]
    public void Top_bar_centers_the_three_Chinese_navigation_actions_on_the_window()
    {
        TestApplicationHost.Ensure();
        using var topBarViewModel = new TopBarViewModel(
            new NavigationViewModel(),
            new RecordingAppearanceService());
        var topBar = new TopBarView { DataContext = topBarViewModel };
        var window = new Window { Width = 1280, Height = 720, Content = topBar };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var navigation = Assert.IsType<StackPanel>(topBar.FindControl<StackPanel>("PageNavigation"));
            var center = Assert.IsType<Point>(navigation.TranslatePoint(
                new Point(navigation.Bounds.Width / 2, navigation.Bounds.Height / 2),
                topBar));
            var labels = navigation.GetLogicalDescendants()
                .OfType<Button>()
                .Select(button => button.Content)
                .ToArray();

            Assert.Equal(["主界面", "设置", "关于"], labels);
            Assert.InRange(center.X, 639.5, 640.5);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Feature_rail_keeps_a_sixteen_pixel_transparent_resize_surface()
    {
        TestApplicationHost.Ensure();
        using var viewModel = new FeatureRailViewModel(
            new RecordingAppearanceService(),
            [new StubBuiltInFeature()]);
        var rail = new FeatureRailView { DataContext = viewModel };

        var grip = Assert.IsType<Border>(rail.FindControl<Border>("SidebarResizeGrip"));

        Assert.Equal(16d, grip.Width);
        Assert.True(grip.IsHitTestVisible);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(grip.Background).Color);
    }

    [Fact]
    public void Status_bar_renders_real_snapshot_meanings()
    {
        TestApplicationHost.Ensure();
        using var receiver = new RecordingReceiverStatusSource(
            SnapshotFactory.Receiver(MoDi.App.Contracts.ReceiverState.Connected) with
            {
                ActiveLink = MoDi.App.Contracts.LinkKind.Lan,
            });
        using var audio = new RecordingAudioSettingsService();
        using var viewModel = new StatusBarViewModel(receiver, audio);
        var statusBar = new StatusBarView { DataContext = viewModel };

        Assert.Equal("已连接", statusBar.FindControl<TextBlock>("ConnectionStatusText")?.Text);
        Assert.Equal(
            "当前链路 LAN · 当前输出 系统默认播放设备",
            statusBar.FindControl<TextBlock>("StatusMessageText")?.Text);
        Assert.Equal("音量 75%", statusBar.FindControl<TextBlock>("EndpointStatusText")?.Text);
    }
}
