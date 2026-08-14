using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingImageSelectionService : IImageSelectionService
{
    public OperationResult<SelectedImage> Result { get; set; } =
        OperationResult<SelectedImage>.Failure("IMAGE_SELECTION_CANCELLED", "未选择图片");
    public int SelectCalls { get; private set; }

    public Task<OperationResult<SelectedImage>> SelectImageAsync(CancellationToken cancellationToken)
    {
        SelectCalls++;
        return Task.FromResult(Result);
    }
}
