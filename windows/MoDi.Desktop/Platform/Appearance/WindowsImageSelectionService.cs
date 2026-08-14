using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Appearance;

public sealed class WindowsImageSelectionService : IImageSelectionService
{
    private readonly Func<IStorageProvider?> _storageProviderAccessor;

    public WindowsImageSelectionService(Func<IStorageProvider?> storageProviderAccessor) =>
        _storageProviderAccessor = storageProviderAccessor
            ?? throw new ArgumentNullException(nameof(storageProviderAccessor));

    public async Task<OperationResult<SelectedImage>> SelectImageAsync(CancellationToken cancellationToken)
    {
        var provider = _storageProviderAccessor();
        if (provider is null)
            return OperationResult<SelectedImage>.Failure("IMAGE_PICKER_UNAVAILABLE", "当前窗口无法打开图片选择器");

        try
        {
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择墨堤背景图片",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("PNG / JPEG 图片")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg"],
                        MimeTypes = ["image/png", "image/jpeg"],
                    },
                ],
            });
            cancellationToken.ThrowIfCancellationRequested();
            var file = files.FirstOrDefault();
            if (file is null)
                return OperationResult<SelectedImage>.Failure("IMAGE_SELECTION_CANCELLED", "未选择图片");

            await using var stream = await file.OpenReadAsync();
            var bytes = await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
                return OperationResult<SelectedImage>.Failure("IMAGE_TOO_LARGE", "背景图片不能超过 20 MiB");
            if (!AppearanceService.TryGetImageExtension(bytes, out _))
                return OperationResult<SelectedImage>.Failure("IMAGE_FORMAT", "只支持 PNG 或 JPEG 图片");
            return OperationResult<SelectedImage>.Success(new SelectedImage(file.Name, bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OperationResult<SelectedImage>.Failure("IMAGE_PICKER", $"读取所选图片失败：{ex.Message}");
        }
    }

    internal static async Task<byte[]?> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return memory.ToArray();
            if (memory.Length + read > AppearanceService.MaximumBackgroundBytes)
                return null;
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
