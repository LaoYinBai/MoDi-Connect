/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using MoDi.Desktop.Composition;
using MoDi.Desktop.Diagnostics;

namespace MoDi.Desktop;

/// <summary>Avalonia 应用入口，加载 MainWindow 为主窗口</summary>
public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private ProductionComposition? _composition;
    private MainWindow? _mainWindow;
    private int _initializationStarted;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnDesktopExit;
            GlobalExceptionBoundary.Observe(
                CreateMainWindowAsync(desktop),
                "App.CreateMainWindow");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CreateMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        MainWindow? window = null;
        var hostContext = new ProductionHostContext(
            () => window?.StorageProvider,
            () => window?.Clipboard,
            CommunityWebsiteUrl: "https://modiconnect.cn");
        _composition = await ProductionComposition.CreateAsync(hostContext, _shutdown.Token);
        window = new MainWindow { DataContext = _composition.Shell };
        _mainWindow = window;
        window.Loaded += OnMainWindowLoaded;
        desktop.MainWindow = window;
        window.Show();
    }

    private async void OnMainWindowLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        if (Interlocked.Exchange(ref _initializationStarted, 1) != 0
            || _composition is null
            || _mainWindow is null)
            return;

        try
        {
            var result = await _composition.InitializeAsync(_shutdown.Token);
            if (!result.IsSuccess)
            {
                var exception = new InvalidOperationException(
                    result.UserMessage ?? "Application initialization returned a failure result.");
                PublishShellError(
                    _mainWindow,
                    GlobalExceptionBoundary.ReportInitializationFailure(exception));
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal application shutdown owns this cancellation.
        }
        catch (Exception exception)
        {
            PublishShellError(
                _mainWindow,
                GlobalExceptionBoundary.ReportInitializationFailure(exception));
        }
    }

    private static void PublishShellError(MainWindow window, InitializationFailure failure)
    {
        try
        {
            if (window.Content is not Control shell)
                return;

            var overlay = new Border
            {
                Margin = new Thickness(24, 64, 24, 0),
                Padding = new Thickness(16, 12),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Background = Brushes.DarkRed,
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"{failure.Code} · {failure.UserMessage}"
                }
            };
            var root = new Grid();
            window.Content = null;
            root.Children.Add(shell);
            root.Children.Add(overlay);
            window.Content = root;
        }
        catch (Exception exception)
        {
            MoDi.Core.Infrastructure.Log.E(
                "App",
                "APP_INITIALIZE_ERROR_PRESENTATION_FAILED",
                exception);
        }
    }

    private static void OnDispatcherUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs eventArgs) =>
        eventArgs.Handled = GlobalExceptionBoundary.HandleDispatcherException(eventArgs.Exception);

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs eventArgs)
    {
        _shutdown.Cancel();
        if (_mainWindow is not null)
            _mainWindow.Loaded -= OnMainWindowLoaded;
        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
        _composition?.Dispose();
        _shutdown.Dispose();
    }
}
