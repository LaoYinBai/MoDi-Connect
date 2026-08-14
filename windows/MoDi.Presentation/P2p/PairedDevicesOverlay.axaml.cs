using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MoDi.Presentation.P2p;

public partial class PairedDevicesOverlay : UserControl
{
    public PairedDevicesOverlay() => InitializeComponent();

    private void OnPointerEntered(object? sender, PointerEventArgs eventArgs) =>
        ViewModel?.Open();

    private void OnPointerExited(object? sender, PointerEventArgs eventArgs) =>
        ViewModel?.Close();

    private void OnAnchorClicked(object? sender, RoutedEventArgs eventArgs) =>
        ViewModel?.Toggle();

    private PairedDevicesViewModel? ViewModel => DataContext as PairedDevicesViewModel;
}
