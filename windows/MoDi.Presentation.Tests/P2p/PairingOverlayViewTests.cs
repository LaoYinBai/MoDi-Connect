using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using MoDi.App.Contracts;
using MoDi.Presentation.P2p;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.P2p;

[Collection("Avalonia UI")]
public sealed class PairingOverlayViewTests
{
    [Fact]
    public void Separate_overlays_preserve_the_accepted_48_pixel_anchors()
    {
        TestApplicationHost.Ensure();
        using var pairing = new RecordingPairingService();
        using var pairedVm = new PairedDevicesViewModel(pairing);
        using var qrVm = new QrPairingViewModel(pairing, TimeProvider.System);
        var paired = new PairedDevicesOverlay { DataContext = pairedVm };
        var qr = new QrPairingOverlay { DataContext = qrVm };
        var window = new Window
        {
            Width = 720,
            Height = 420,
            Content = new Grid { Children = { paired, qr } }
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var deviceAnchor = Assert.IsType<Border>(paired.FindControl<Border>("PairedDevicesAnchor"));
            var qrAnchor = Assert.IsType<Border>(qr.FindControl<Border>("QrAnchor"));
            Assert.Equal(48d, deviceAnchor.Width);
            Assert.Equal(48d, deviceAnchor.Height);
            Assert.Equal(48d, qrAnchor.Width);
            Assert.Equal(48d, qrAnchor.Height);
            Assert.Equal(24d, deviceAnchor.CornerRadius.TopLeft);
            Assert.Equal(24d, qrAnchor.CornerRadius.TopLeft);
            Assert.Equal(Color.Parse("#47937F"), Assert.IsAssignableFrom<ISolidColorBrush>(deviceAnchor.BorderBrush).Color);
            Assert.Equal(Color.Parse("#E8863C"), Assert.IsAssignableFrom<ISolidColorBrush>(qrAnchor.BorderBrush).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Overlay_actions_are_bound_to_the_focused_view_models()
    {
        TestApplicationHost.Ensure();
        using var pairing = new RecordingPairingService();
        using var pairedVm = new PairedDevicesViewModel(pairing);
        using var qrVm = new QrPairingViewModel(pairing, TimeProvider.System);
        var paired = new PairedDevicesOverlay { DataContext = pairedVm };
        var qr = new QrPairingOverlay { DataContext = qrVm };
        var window = new Window
        {
            Content = new StackPanel { Children = { paired, qr } }
        };

        try
        {
            window.Show();
            pairedVm.Open();
            qrVm.Open();
            Dispatcher.UIThread.RunJobs();

            var reconnect = Assert.IsType<Button>(paired.GetLogicalDescendants()
                .OfType<Button>()
                .First(button => button.Name == "PairedDeviceButton"));
            var refresh = Assert.IsType<Button>(qr.FindControl<Button>("RefreshQrButton"));
            Assert.Same(pairedVm.ConnectCommand, reconnect.Command);
            Assert.Same(qrVm.RefreshCommand, refresh.Command);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Background_pairing_snapshot_is_marshaled_to_the_ui_thread_when_view_model_was_created_off_thread()
    {
        TestApplicationHost.Ensure();
        using var pairing = new RecordingPairingService();
        PairedDevicesViewModel? created = null;
        RunOnWorker(() => created = new PairedDevicesViewModel(pairing));
        using var pairedVm = Assert.IsType<PairedDevicesViewModel>(created);
        var paired = new PairedDevicesOverlay { DataContext = pairedVm };
        var window = new Window { Content = paired };

        try
        {
            window.Show();
            pairedVm.Open();
            Dispatcher.UIThread.RunJobs();

            RunOnWorker(() => pairing.Publish(SnapshotFactory.Pairing(devices:
            [
                new PairedDeviceSnapshot("recent-p2p", "后台更新设备", "刚刚")
            ])));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("后台更新设备", Assert.Single(pairedVm.Devices).DisplayName);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Each_overlay_owns_only_its_accepted_icon_and_popover()
    {
        TestApplicationHost.Ensure();
        var paired = new PairedDevicesOverlay();
        var qr = new QrPairingOverlay();

        var pairedIcon = Assert.IsType<MaterialIcon>(paired.FindControl<MaterialIcon>("PairedDevicesIcon"));
        var qrIcon = Assert.IsType<MaterialIcon>(qr.FindControl<MaterialIcon>("QrIcon"));

        Assert.Equal(MaterialIconKind.Cellphone, pairedIcon.Kind);
        Assert.Equal(MaterialIconKind.ViewGridOutline, qrIcon.Kind);
        Assert.Equal(22d, pairedIcon.Width);
        Assert.Equal(22d, qrIcon.Width);
        Assert.Null(paired.FindControl<Control>("QrOverlay"));
        Assert.Null(qr.FindControl<Control>("PairedDevicesOverlay"));
    }

    private static void RunOnWorker(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw failure;
    }
}
