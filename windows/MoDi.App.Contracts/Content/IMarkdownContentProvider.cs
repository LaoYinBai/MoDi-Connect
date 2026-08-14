namespace MoDi.App.Contracts;

public interface IMarkdownContentProvider
{
    Task<OperationResult<string>> GetAsync(
        MarkdownContentKey key,
        CancellationToken cancellationToken);
}
