using System.Text.RegularExpressions;
using MoDi.App.Contracts;

namespace MoDi.Presentation.Markdown;

public static partial class SafeMarkdownParser
{
    public static OperationResult<MarkdownDocument> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var unsafeResult = ValidateSafety(markdown);
        if (unsafeResult is not null)
            return OperationResult<MarkdownDocument>.Failure(unsafeResult.Value.Code, unsafeResult.Value.Message);

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var blocks = new List<MarkdownBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            var line = lines[index];
            if (TryFence(line, out var fence, out var language))
            {
                index++;
                var codeLines = new List<string>();
                while (index < lines.Length && !lines[index].TrimStart().StartsWith(fence, StringComparison.Ordinal))
                    codeLines.Add(lines[index++]);

                if (index >= lines.Length)
                    return OperationResult<MarkdownDocument>.Failure(
                        "MARKDOWN_CODE_UNTERMINATED",
                        "代码块没有结束标记");

                index++;
                blocks.Add(new CodeBlock(language, string.Join("\n", codeLines)));
                continue;
            }

            if (TryHeading(line, out var level, out var headingText))
            {
                blocks.Add(new HeadingBlock(level, ParseInlines(headingText)));
                index++;
                continue;
            }

            if (IsListLine(line))
            {
                var items = new List<MarkdownListItem>();
                while (index < lines.Length && IsListLine(lines[index]))
                {
                    items.Add(new MarkdownListItem(ParseInlines(lines[index].TrimStart()[2..].Trim())));
                    index++;
                }

                blocks.Add(new ListBlock(items));
                continue;
            }

            if (IsQuoteLine(line))
            {
                var quoteLines = new List<string>();
                while (index < lines.Length && IsQuoteLine(lines[index]))
                {
                    quoteLines.Add(lines[index].TrimStart()[1..].TrimStart());
                    index++;
                }

                blocks.Add(new QuoteBlock(ParseInlines(string.Join(" ", quoteLines))));
                continue;
            }

            var paragraphLines = new List<string>();
            while (index < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[index]) &&
                   !TryFence(lines[index], out _, out _) &&
                   !TryHeading(lines[index], out _, out _) &&
                   !IsListLine(lines[index]) &&
                   !IsQuoteLine(lines[index]))
            {
                paragraphLines.Add(lines[index].Trim());
                index++;
            }

            blocks.Add(new ParagraphBlock(ParseInlines(string.Join(" ", paragraphLines))));
        }

        return OperationResult<MarkdownDocument>.Success(new MarkdownDocument(blocks));
    }

    private static (string Code, string Message)? ValidateSafety(string markdown)
    {
        if (IncludePattern().IsMatch(markdown))
            return ("MARKDOWN_INCLUDE_FORBIDDEN", "Markdown 不允许 include 指令");

        if (ImagePattern().IsMatch(markdown))
            return ("MARKDOWN_IMAGE_FORBIDDEN", "Markdown 不允许图片语法");

        if (RawHtmlPattern().IsMatch(markdown))
            return ("MARKDOWN_HTML_FORBIDDEN", "Markdown 不允许原始 HTML");

        foreach (Match match in LinkPattern().Matches(markdown))
        {
            var target = match.Groups[2].Value.Trim();
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return ("MARKDOWN_LINK_SCHEME", "Markdown 链接只允许 HTTPS");
            }
        }

        return null;
    }

    private static IReadOnlyList<MarkdownInline> ParseInlines(string text)
    {
        var inlines = new List<MarkdownInline>();
        var index = 0;

        while (index < text.Length)
        {
            if (text.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    inlines.Add(new StrongInline(ParseInlines(text[(index + 2)..end])));
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] == '*')
            {
                var end = text.IndexOf('*', index + 1);
                if (end >= 0)
                {
                    inlines.Add(new EmphasisInline(ParseInlines(text[(index + 1)..end])));
                    index = end + 1;
                    continue;
                }
            }

            if (text[index] == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end >= 0)
                {
                    inlines.Add(new InlineCode(text[(index + 1)..end]));
                    index = end + 1;
                    continue;
                }
            }

            if (text[index] == '[')
            {
                var labelEnd = text.IndexOf("](", index, StringComparison.Ordinal);
                var targetEnd = labelEnd >= 0 ? text.IndexOf(')', labelEnd + 2) : -1;
                if (labelEnd >= 0 && targetEnd >= 0)
                {
                    var target = text[(labelEnd + 2)..targetEnd];
                    if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
                    {
                        inlines.Add(new LinkInline(ParseInlines(text[(index + 1)..labelEnd]), uri));
                        index = targetEnd + 1;
                        continue;
                    }
                }
            }

            var next = index + 1;
            while (next < text.Length && text[next] is not '*' and not '`' and not '[')
                next++;
            inlines.Add(new TextInline(text[index..next]));
            index = next;
        }

        return inlines;
    }

    private static bool TryFence(string line, out string fence, out string language)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("~~~", StringComparison.Ordinal))
            fence = "~~~";
        else if (trimmed.StartsWith("```", StringComparison.Ordinal))
            fence = "```";
        else
        {
            fence = string.Empty;
            language = string.Empty;
            return false;
        }

        language = trimmed[fence.Length..].Trim();
        return true;
    }

    private static bool TryHeading(string line, out int level, out string text)
    {
        var trimmed = line.TrimStart();
        level = 0;
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
            level++;

        if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
        {
            level = 0;
            text = string.Empty;
            return false;
        }

        text = trimmed[(level + 1)..].Trim();
        return true;
    }

    private static bool IsListLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length >= 2 && trimmed[0] is '-' or '*' && trimmed[1] == ' ';
    }

    private static bool IsQuoteLine(string line) => line.TrimStart().StartsWith(">", StringComparison.Ordinal);

    [GeneratedRegex("(?im)^\\s*!include\\b")]
    private static partial Regex IncludePattern();

    [GeneratedRegex("!\\[[^\\]]*\\]\\s*\\(")]
    private static partial Regex ImagePattern();

    [GeneratedRegex("<[^>\\r\\n]*>")]
    private static partial Regex RawHtmlPattern();

    [GeneratedRegex("(?<!!)\\[([^\\]]*)\\]\\(([^)\\s]+)(?:\\s+\"[^\"]*\")?\\)")]
    private static partial Regex LinkPattern();
}
