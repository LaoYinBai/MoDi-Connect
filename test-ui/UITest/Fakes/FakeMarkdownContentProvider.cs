using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeMarkdownContentProvider : IMarkdownContentProvider
{
    private readonly IReadOnlyDictionary<MarkdownContentKey, string> _documents =
        new Dictionary<MarkdownContentKey, string>
        {
            [MarkdownContentKey.Stories] = "# 故事汇\n\n声音从桥上过，小男孩在桥头等。",
            [MarkdownContentKey.TechnicalSupport] = "# 技术支持\n\n- 说明复现步骤\n- 附上脱敏日志",
            [MarkdownContentKey.Sponsors] = "# 赞助列表\n\n> 感谢每一位同行者。",
            [MarkdownContentKey.ReleaseNotes] = "# 发行说明\n\n首个共享 UI 演示版本。",
            [MarkdownContentKey.ThirdPartyNotices] = "# 第三方声明\n\n霞鹜文楷采用 SIL Open Font License 1.1。",
        };

    public Task<OperationResult<string>> GetAsync(MarkdownContentKey key, CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult<string>.Success(_documents[key]));
}
