using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Storage;

namespace MoDi.Desktop.Platform.Appearance;

internal interface IAppearanceResetTarget
{
    Task<OperationResult> ResetToDefaultsAsync(CancellationToken cancellationToken);
}

public sealed class AppearanceService : IAppearanceService, IAppearanceResetTarget
{
    internal const int MaximumBackgroundBytes = 20 * 1024 * 1024;
    private readonly ApplicationDataPaths _paths;
    private readonly AtomicJsonStore<AppearanceSettingsV1> _store;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SynchronizationContext? _uiContext;
    private AppearanceSettingsV1 _settings;

    private AppearanceService(
        ApplicationDataPaths paths,
        TimeProvider timeProvider,
        AppearanceSettingsV1 settings,
        SynchronizationContext? uiContext)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _store = new AtomicJsonStore<AppearanceSettingsV1>(
            paths.AppearanceSettingsFile,
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)),
            AppearanceSettingsV1.JsonOptions);
        _uiContext = uiContext;
        _settings = settings;
        Snapshot = _settings.ToSnapshot();
    }

    public AppearanceService(ApplicationDataPaths paths)
        : this(paths, TimeProvider.System, AppearanceSettingsV1.Default, SynchronizationContext.Current) { }

    internal static async Task<AppearanceService> CreateAsync(
        ApplicationDataPaths paths,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var callerContext = SynchronizationContext.Current;
        var store = new AtomicJsonStore<AppearanceSettingsV1>(
            paths.AppearanceSettingsFile,
            timeProvider,
            AppearanceSettingsV1.JsonOptions);
        AppearanceSettingsV1? loaded;
        try
        {
            loaded = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loaded = null;
        }

        var settings = loaded is { SchemaVersion: AppearanceSettingsV1.CurrentSchemaVersion }
            ? loaded with { FeatureRailWidth = loaded.FeatureRailWidth <= 128 ? 56d : 200d }
            : AppearanceSettingsV1.Default;
        return new AppearanceService(paths, timeProvider, settings, callerContext);
    }

    public AppearanceSnapshot Snapshot { get; private set; }
    public event Action<AppearanceSnapshot>? SnapshotChanged;

    public Task<OperationResult> SelectPresetAsync(ThemePreset preset, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(preset))
            return Task.FromResult(OperationResult.Failure("APPEARANCE_PRESET", "无法识别所选主题"));
        return UpdateAsync(settings => settings with { Preset = preset }, cancellationToken);
    }

    public Task<OperationResult> SaveCustomPaletteAsync(
        CustomPalette palette,
        CancellationToken cancellationToken)
    {
        if (palette is null || !IsValidPalette(palette))
            return Task.FromResult(OperationResult.Failure(
                "APPEARANCE_COLOR",
                "自定义颜色必须使用 #RRGGBB 或 #AARRGGBB 格式"));
        return UpdateAsync(
            settings => settings with { Preset = ThemePreset.Custom, Palette = palette },
            cancellationToken);
    }

    public async Task<OperationResult> ImportBackgroundAsync(
        SelectedImage image,
        CancellationToken cancellationToken)
    {
        var bytes = image.PngOrJpegBytes;
        if (bytes.IsEmpty || bytes.Length > MaximumBackgroundBytes || !TryGetImageExtension(bytes.Span, out var extension))
            return OperationResult.Failure("APPEARANCE_IMAGE_FORMAT", "背景图片必须是 20 MiB 以内的 PNG 或 JPEG");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fileName = "background" + extension;
            var targetPath = Path.Combine(_paths.AppearanceDirectory, fileName);
            await WriteBackgroundAtomicallyAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
            var next = _settings with { BackgroundFileName = fileName };
            await _store.WriteAsync(next, cancellationToken).ConfigureAwait(false);
            DeleteOtherBackgroundFiles(fileName);
            Apply(next);
            return OperationResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return OperationResult.Failure("APPEARANCE_STORAGE", $"保存背景图片失败：{ex.Message}");
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public Task<OperationResult> SetReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken) =>
        UpdateAsync(settings => settings with { ReduceMotion = reduceMotion }, cancellationToken);

    public Task<OperationResult> SetFeatureRailWidthAsync(double width, CancellationToken cancellationToken)
    {
        var normalized = double.IsFinite(width) && width <= 128 ? 56d : 200d;
        return UpdateAsync(settings => settings with { FeatureRailWidth = normalized }, cancellationToken);
    }

    async Task<OperationResult> IAppearanceResetTarget.ResetToDefaultsAsync(CancellationToken cancellationToken) =>
        await ResetToDefaultsAsync(cancellationToken).ConfigureAwait(false);

    internal async Task<OperationResult> ResetToDefaultsAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeleteOwnedBackgroundFiles();
            await _store.WriteAsync(AppearanceSettingsV1.Default, cancellationToken).ConfigureAwait(false);
            Apply(AppearanceSettingsV1.Default);
            return OperationResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return OperationResult.Failure("APPEARANCE_RESET", $"重置个性化失败：{ex.Message}");
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task<OperationResult> UpdateAsync(
        Func<AppearanceSettingsV1, AppearanceSettingsV1> update,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var next = update(_settings);
            await _store.WriteAsync(next, cancellationToken).ConfigureAwait(false);
            Apply(next);
            return OperationResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return OperationResult.Failure("APPEARANCE_STORAGE", $"保存个性化设置失败：{ex.Message}");
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void Apply(AppearanceSettingsV1 settings)
    {
        _settings = settings;
        Snapshot = settings.ToSnapshot();
        var publishedSnapshot = Snapshot;
        var handler = SnapshotChanged;
        if (handler is null)
            return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            handler(publishedSnapshot);
        else
            _uiContext.Post(_ => handler(publishedSnapshot), null);
    }

    private static bool IsValidPalette(CustomPalette palette) =>
        IsHexColor(palette.Background)
        && IsHexColor(palette.Surface)
        && IsHexColor(palette.SurfaceElevated)
        && IsHexColor(palette.TextPrimary)
        && IsHexColor(palette.TextSecondary)
        && IsHexColor(palette.Accent)
        && IsHexColor(palette.Border)
        && IsHexColor(palette.Success);

    private static bool IsHexColor(string? value)
    {
        if (value is null || value[0] != '#' || value.Length is not (7 or 9))
            return false;
        for (var index = 1; index < value.Length; index++)
            if (!Uri.IsHexDigit(value[index]))
                return false;
        return true;
    }

    internal static bool TryGetImageExtension(ReadOnlySpan<byte> bytes, out string extension)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            extension = ".png";
            return true;
        }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }
        extension = string.Empty;
        return false;
    }

    private static async Task WriteBackgroundAtomicallyAsync(
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = targetPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private void DeleteOtherBackgroundFiles(string keepFileName)
    {
        foreach (var fileName in new[] { "background.png", "background.jpg", "background.jpeg" })
            if (!string.Equals(fileName, keepFileName, StringComparison.OrdinalIgnoreCase))
                DeleteIfExists(Path.Combine(_paths.AppearanceDirectory, fileName));
    }

    private void DeleteOwnedBackgroundFiles()
    {
        foreach (var fileName in new[] { "background.png", "background.jpg", "background.jpeg" })
            DeleteIfExists(Path.Combine(_paths.AppearanceDirectory, fileName));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
