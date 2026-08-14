using System.Collections.ObjectModel;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class PluginManagerCardViewModel : ObservableObject, IDisposable
{
    private readonly IPluginCatalogService _plugins;
    private readonly ObservableCollection<PluginEntryViewModel> _entries = [];
    private bool _canImportExternal;
    private string _capabilityMessage = string.Empty;
    private string? _feedbackText;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public PluginManagerCardViewModel(IPluginCatalogService plugins)
    {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        Entries = new ReadOnlyObservableCollection<PluginEntryViewModel>(_entries);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => CanImportExternal && !_disposed);
        ToggleEnabledCommand = new AsyncRelayCommand<PluginEntryViewModel>(
            ToggleEnabledAsync,
            entry => entry is not null && !_disposed);
        UninstallCommand = new AsyncRelayCommand<PluginEntryViewModel>(
            UninstallAsync,
            entry => entry is { CanUninstall: true, IsBuiltIn: false } && !_disposed);
        ApplySnapshot(plugins.Snapshot);
        plugins.SnapshotChanged += OnSnapshotChanged;
    }

    public ReadOnlyObservableCollection<PluginEntryViewModel> Entries { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand<PluginEntryViewModel> ToggleEnabledCommand { get; }
    public AsyncRelayCommand<PluginEntryViewModel> UninstallCommand { get; }

    public bool CanImportExternal
    {
        get => _canImportExternal;
        private set
        {
            if (SetProperty(ref _canImportExternal, value))
                ImportCommand.RaiseCanExecuteChanged();
        }
    }

    public string CapabilityMessage { get => _capabilityMessage; private set => SetProperty(ref _capabilityMessage, value); }
    public string? FeedbackText { get => _feedbackText; private set => SetProperty(ref _feedbackText, value); }
    public string? ErrorCode { get => _errorCode; private set => SetProperty(ref _errorCode, value); }
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _plugins.SnapshotChanged -= OnSnapshotChanged;
        ImportCommand.RaiseCanExecuteChanged();
        ToggleEnabledCommand.RaiseCanExecuteChanged();
        UninstallCommand.RaiseCanExecuteChanged();
    }

    private Task ImportAsync(CancellationToken cancellationToken) => RunOperationAsync(
        token => _plugins.ImportAsync(token),
        "PRESENTATION_PLUGIN_IMPORT",
        "无法导入插件",
        "插件导入已完成",
        cancellationToken);

    private Task ToggleEnabledAsync(PluginEntryViewModel? entry, CancellationToken cancellationToken) =>
        entry is null
            ? Task.CompletedTask
            : RunOperationAsync(
                token => _plugins.SetEnabledAsync(entry.Id, !entry.IsEnabled, token),
                "PRESENTATION_PLUGIN_TOGGLE",
                "无法更改插件状态",
                entry.IsEnabled ? "插件已停用" : "插件已启用",
                cancellationToken);

    private Task UninstallAsync(PluginEntryViewModel? entry, CancellationToken cancellationToken) =>
        entry is null || entry.IsBuiltIn || !entry.CanUninstall
            ? Task.CompletedTask
            : RunOperationAsync(
                token => _plugins.UninstallAsync(entry.Id, token),
                "PRESENTATION_PLUGIN_UNINSTALL",
                "无法卸载插件",
                "插件已卸载",
                cancellationToken);

    private async Task RunOperationAsync(
        Func<CancellationToken, Task<OperationResult>> operation,
        string exceptionCode,
        string exceptionMessage,
        string successMessage,
        CancellationToken cancellationToken)
    {
        SetError(null, null);
        try
        {
            var result = await operation(cancellationToken);
            if (!result.IsSuccess)
            {
                SetError(result.ErrorCode, result.UserMessage);
                return;
            }

            FeedbackText = successMessage;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetError(exceptionCode, exceptionMessage);
        }
    }

    private void OnSnapshotChanged(PluginCatalogSnapshot snapshot) => ApplySnapshot(snapshot);

    private void ApplySnapshot(PluginCatalogSnapshot snapshot)
    {
        if (_disposed)
            return;

        CanImportExternal = snapshot.CanImportExternal;
        CapabilityMessage = snapshot.CapabilityMessage;
        _entries.Clear();
        foreach (var entry in snapshot.Entries)
        {
            _entries.Add(new PluginEntryViewModel(
                entry.Id,
                entry.DisplayName,
                entry.IsBuiltIn,
                entry.IsEnabled,
                entry.CanUninstall,
                entry.Health,
                entry.Detail,
                entry.Developer));
        }

        ToggleEnabledCommand.RaiseCanExecuteChanged();
        UninstallCommand.RaiseCanExecuteChanged();
    }

    private void SetError(string? code, string? message)
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}

public sealed record PluginEntryViewModel(
    string Id,
    string DisplayName,
    bool IsBuiltIn,
    bool IsEnabled,
    bool CanUninstall,
    PluginHealth Health,
    string Detail,
    PluginDeveloperMetadata Developer)
{
    public string BuiltInLabel => IsBuiltIn ? "内置插件 · 不可卸载" : "外部插件";
}
