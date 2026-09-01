using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Content;

public sealed class EmbeddedMarkdownContentProvider(Assembly assembly) : IMarkdownContentProvider
{
    private readonly Assembly _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));

    public async Task<OperationResult<string>> GetAsync(
        MarkdownContentKey key,
        CancellationToken cancellationToken)
    {
        var name = ResourceName(key);
        try
        {
            await using var stream = _assembly.GetManifestResourceStream(name);
            if (stream is null)
                return OperationResult<string>.Failure("CONTENT_NOT_PACKAGED", "应用内文档未随程序打包");
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return OperationResult<string>.Failure("CONTENT_READ", $"读取应用内文档失败：{ex.Message}");
        }
    }

    internal static string ResourceName(MarkdownContentKey key) => key switch
    {
        MarkdownContentKey.Stories => "MoDi.Desktop.Content.Stories.md",
        MarkdownContentKey.StoryOrigin => "MoDi.Desktop.Content.Stories.Origin.md",
        MarkdownContentKey.StoryCurrentChapter => "MoDi.Desktop.Content.Stories.CurrentChapter.md",
        MarkdownContentKey.StoryInkBridge => "MoDi.Desktop.Content.Stories.InkBridge.md",
        MarkdownContentKey.TechnicalSupport => "MoDi.Desktop.Content.TechnicalSupport.md",
        MarkdownContentKey.SupportUpdates => "MoDi.Desktop.Content.Support.Updates.md",
        MarkdownContentKey.SupportConnections => "MoDi.Desktop.Content.Support.Connections.md",
        MarkdownContentKey.SupportDiagnostics => "MoDi.Desktop.Content.Support.Diagnostics.md",
        MarkdownContentKey.Sponsors => "MoDi.Desktop.Content.Sponsors.md",
        MarkdownContentKey.ReleaseNotes => "MoDi.Desktop.Content.ReleaseNotes.md",
        MarkdownContentKey.ThirdPartyNotices => "MoDi.Desktop.Content.ThirdPartyNotices.md",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知应用内文档"),
    };
}
