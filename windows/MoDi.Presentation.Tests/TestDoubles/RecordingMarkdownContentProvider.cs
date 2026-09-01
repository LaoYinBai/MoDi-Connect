using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingMarkdownContentProvider : IMarkdownContentProvider
{
    private readonly Dictionary<MarkdownContentKey, string> _documents = new()
    {
        [MarkdownContentKey.Stories] = "# 故事汇\n\n桥上的声音。",
        [MarkdownContentKey.StoryOrigin] = "# 为什么叫墨堤\n\n名字的来处。",
        [MarkdownContentKey.StoryCurrentChapter] = "# 当前这一章\n\nV1 的故事。",
        [MarkdownContentKey.StoryInkBridge] = "# 水墨桥的语法\n\n状态机是真相。",
        [MarkdownContentKey.TechnicalSupport] = "# 技术支持\n\n请附上日志。",
        [MarkdownContentKey.SupportUpdates] = "# 版本与更新\n\n更新说明。",
        [MarkdownContentKey.SupportConnections] = "# 连接排查\n\n连接说明。",
        [MarkdownContentKey.SupportDiagnostics] = "# 日志与诊断\n\n诊断说明。",
        [MarkdownContentKey.Sponsors] = "# 赞助列表\n\n感谢同行者。",
        [MarkdownContentKey.ReleaseNotes] = "# 发行说明\n\n首个版本。",
        [MarkdownContentKey.ThirdPartyNotices] = "# 第三方声明\n\n字体许可。",
    };

    public List<MarkdownContentKey> RequestedKeys { get; } = [];
    public OperationResult<string>? ResultOverride { get; set; }

    public Task<OperationResult<string>> GetAsync(MarkdownContentKey key, CancellationToken cancellationToken)
    {
        RequestedKeys.Add(key);
        return Task.FromResult(ResultOverride ?? OperationResult<string>.Success(_documents[key]));
    }

    public void Set(MarkdownContentKey key, string markdown) => _documents[key] = markdown;
}
