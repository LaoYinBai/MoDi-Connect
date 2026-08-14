using Avalonia.Controls;
using Avalonia.Input;

namespace MoDi.Presentation.Shell;

public partial class FeatureRailView : UserControl
{
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartWidth;

    public FeatureRailView()
    {
        InitializeComponent();
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    private void OnResizeGripPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (DataContext is not FeatureRailViewModel viewModel ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        _dragStartX = eventArgs.GetPosition(this).X;
        _dragStartWidth = viewModel.Width;
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (!_dragging || DataContext is not FeatureRailViewModel viewModel)
            return;

        var delta = eventArgs.GetPosition(this).X - _dragStartX;
        viewModel.PreviewWidth(_dragStartWidth + delta);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (!_dragging)
            return;

        _dragging = false;
        eventArgs.Pointer.Capture(null);
        if (DataContext is FeatureRailViewModel viewModel)
            viewModel.CommitWidthCommand.Execute(null);
    }
}
