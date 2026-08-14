using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MoDi.Presentation.Shell;

public partial class TopBarView : UserControl
{
    public TopBarView() => InitializeComponent();

    public event EventHandler? MinimizeRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<PointerPressedEventArgs>? DragRequested;

    private void OnMinimizeClicked(object? sender, RoutedEventArgs eventArgs) =>
        MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClicked(object? sender, RoutedEventArgs eventArgs) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs eventArgs) =>
        DragRequested?.Invoke(this, eventArgs);
}
