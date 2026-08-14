using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace MoDi.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShellHost.MinimizeRequested += OnMinimizeRequested;
        ShellHost.CloseRequested += OnCloseRequested;
        ShellHost.DragRequested += OnDragRequested;
        Closed += OnClosed;
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
    }
}
