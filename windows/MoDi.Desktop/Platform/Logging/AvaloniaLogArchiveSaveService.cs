using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Logging;

public sealed class AvaloniaLogArchiveSaveService : ILogArchiveSaveService
{
    private static readonly FilePickerFileType ZipFileType = new("ZIP 压缩包")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip"],
    };

    private readonly Func<IStorageProvider?> _storageProviderAccessor;

    public AvaloniaLogArchiveSaveService(Func<IStorageProvider?> storageProviderAccessor) =>
        _storageProviderAccessor = storageProviderAccessor
            ?? throw new ArgumentNullException(nameof(storageProviderAccessor));

    public async Task<OperationResult<string>> SaveAsync(
        string suggestedFileName,
        string sourceArchivePath,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return await SaveOnUiThreadAsync(suggestedFileName, sourceArchivePath, cancellationToken);

        var operation = Dispatcher.UIThread.InvokeAsync(
            () => SaveOnUiThreadAsync(suggestedFileName, sourceArchivePath, cancellationToken));
        return await operation;
    }

    private async Task<OperationResult<string>> SaveOnUiThreadAsync(
        string suggestedFileName,
        string sourceArchivePath,
        CancellationToken cancellationToken)
    {
        var provider = _storageProviderAccessor();
        if (provider is null)
            return OperationResult<string>.Failure("LOG_EXPORT_PICKER_UNAVAILABLE", "当前窗口无法打开保存位置选择器");

        try
        {
            var downloads = await provider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads);
            var selected = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出墨堤诊断日志",
                SuggestedFileName = suggestedFileName,
                SuggestedStartLocation = downloads,
                DefaultExtension = "zip",
                FileTypeChoices = [ZipFileType],
                SuggestedFileType = ZipFileType,
                ShowOverwritePrompt = true,
            });
            cancellationToken.ThrowIfCancellationRequested();
            if (selected is null)
                return OperationResult<string>.Failure("LOG_EXPORT_CANCELLED", "已取消导出");

            await using var source = File.OpenRead(sourceArchivePath);
            await using var destination = await selected.OpenWriteAsync();
            destination.SetLength(0);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(selected.Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Failure("LOG_EXPORT_SAVE", $"保存日志失败：{ex.Message}");
        }
    }
}
