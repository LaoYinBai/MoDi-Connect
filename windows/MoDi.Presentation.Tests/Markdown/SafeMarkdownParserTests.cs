using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.Tests.Markdown;

public sealed class SafeMarkdownParserTests
{
    [Fact]
    public void Parses_the_approved_subset()
    {
        const string markdown = "# 标题\n\n正文 **强调**。\n\n- 条目\n\n> 引用\n\n~~~text\ncode\n~~~";

        var result = SafeMarkdownParser.Parse(markdown);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!.Blocks,
            block => Assert.IsType<HeadingBlock>(block),
            block => Assert.IsType<ParagraphBlock>(block),
            block => Assert.IsType<ListBlock>(block),
            block => Assert.IsType<QuoteBlock>(block),
            block => Assert.IsType<CodeBlock>(block));
        var paragraph = Assert.IsType<ParagraphBlock>(result.Value.Blocks[1]);
        Assert.Contains(paragraph.Inlines, inline => inline is StrongInline);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "MARKDOWN_HTML_FORBIDDEN")]
    [InlineData("![x](file:///c:/secret.txt)", "MARKDOWN_IMAGE_FORBIDDEN")]
    [InlineData("[x](javascript:alert(1))", "MARKDOWN_LINK_SCHEME")]
    [InlineData("[x](http://example.com)", "MARKDOWN_LINK_SCHEME")]
    [InlineData("[x](../secret.txt)", "MARKDOWN_LINK_SCHEME")]
    [InlineData("!include ../../secret", "MARKDOWN_INCLUDE_FORBIDDEN")]
    public void Rejects_unsafe_constructs(string markdown, string code)
    {
        var result = SafeMarkdownParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public void Safe_https_links_are_closed_inline_tokens()
    {
        var result = SafeMarkdownParser.Parse("访问 [墨堤](https://example.com/project)。");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(result.Value!.Blocks));
        var link = Assert.IsType<LinkInline>(Assert.Single(paragraph.Inlines, inline => inline is LinkInline));
        Assert.Equal("https", link.Uri.Scheme);
    }
}
