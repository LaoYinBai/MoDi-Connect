using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Content;

public sealed class AvaloniaClipboardService(Func<IClipboard?> clipboardAccessor) : IClipboardService
{
    private readonly Func<IClipboard?> _clipboardAccessor = clipboardAccessor
        ?? throw new ArgumentNullException(nameof(clipboardAccessor));

    public async Task<OperationResult> CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clipboard = _clipboardAccessor();
        if (clipboard is null)
            return OperationResult.Failure("CLIPBOARD_UNAVAILABLE", "当前窗口无法使用剪贴板");
        try
        {
            await clipboard.SetTextAsync(text ?? string.Empty);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("CLIPBOARD_WRITE", $"复制文本失败：{ex.Message}");
        }
    }
}
