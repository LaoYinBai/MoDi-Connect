using MoDi.App.Contracts;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class RecordingImageSelectionService : IImageSelectionService
{
    public int Calls { get; private set; }
    public OperationResult<SelectedImage> Result { get; set; } =
        OperationResult<SelectedImage>.Failure("TEST_NO_IMAGE", "未配置测试图片");

    public Task<OperationResult<SelectedImage>> SelectImageAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
