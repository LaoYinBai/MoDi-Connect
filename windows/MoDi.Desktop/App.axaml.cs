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
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MoDi.Desktop.Composition;

namespace MoDi.Desktop;

/// <summary>Avalonia 应用入口，加载 MainWindow 为主窗口</summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow? window = null;
            var hostContext = new ProductionHostContext(
                () => window?.StorageProvider,
                () => window?.Clipboard,
                CommunityWebsiteUrl: "https://modiconnect.cn");
            var composition = ProductionComposition.Create(hostContext);
            window = new MainWindow { DataContext = composition.Shell };
            var initialized = false;
            window.Loaded += async (_, _) =>
            {
                if (initialized)
                    return;
                initialized = true;
                await composition.InitializeAsync(CancellationToken.None);
            };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => composition.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
