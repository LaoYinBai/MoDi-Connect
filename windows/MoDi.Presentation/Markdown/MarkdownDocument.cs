namespace MoDi.Presentation.Markdown;

public sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks)
{
    public static MarkdownDocument Empty { get; } = new([]);
}
