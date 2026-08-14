using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using UITest.Demo;

namespace UITest;

public partial class MainWindow : Window
{
    private readonly TestUiComposition _composition;

    public MainWindow() : this(new TestUiComposition(TimeProvider.System))
    {
    }

    public MainWindow(TestUiComposition composition)
    {
        InitializeComponent();
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        DataContext = composition;
        ShellHost.MinimizeRequested += OnMinimizeRequested;
        ShellHost.CloseRequested += OnCloseRequested;
        ShellHost.DragRequested += OnDragRequested;
        Closed += OnClosed;
    }

    private void OnMinimizeRequested(object? sender, EventArgs eventArgs) =>
        WindowState = WindowState.Minimized;

    private void OnCloseRequested(object? sender, EventArgs eventArgs) => Close();

    private void OnDragRequested(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.Source is Visual source && source.FindAncestorOfType<Button>() is not null)
            return;

        if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(eventArgs);
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        ShellHost.MinimizeRequested -= OnMinimizeRequested;
        ShellHost.CloseRequested -= OnCloseRequested;
        ShellHost.DragRequested -= OnDragRequested;
        Closed -= OnClosed;
        _composition.Dispose();
    }
}
