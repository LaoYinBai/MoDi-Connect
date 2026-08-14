using Avalonia.Controls;
using Avalonia.Input;

namespace MoDi.Presentation.P2p;

public partial class QrPairingOverlay : UserControl
{
    public QrPairingOverlay() => InitializeComponent();

    private void OnPointerEntered(object? sender, PointerEventArgs eventArgs) =>
        ViewModel?.Open();

    private void OnPointerExited(object? sender, PointerEventArgs eventArgs) =>
        ViewModel?.Close();

    private QrPairingViewModel? ViewModel => DataContext as QrPairingViewModel;
}
