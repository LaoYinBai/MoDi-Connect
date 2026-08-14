namespace MoDi.App.Contracts;

public interface IImageSelectionService
{
    Task<OperationResult<SelectedImage>> SelectImageAsync(CancellationToken cancellationToken);
}
