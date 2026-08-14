namespace MoDi.Presentation.Markdown;

public abstract record MarkdownBlock;

public sealed record HeadingBlock(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record ParagraphBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record ListBlock(IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

public sealed record QuoteBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record CodeBlock(string Language, string Text) : MarkdownBlock;

public sealed record MarkdownListItem(IReadOnlyList<MarkdownInline> Inlines);

public abstract record MarkdownInline;

public sealed record TextInline(string Text) : MarkdownInline;

public sealed record EmphasisInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record StrongInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record InlineCode(string Text) : MarkdownInline;

public sealed record LinkInline(IReadOnlyList<MarkdownInline> Inlines, Uri Uri) : MarkdownInline;
