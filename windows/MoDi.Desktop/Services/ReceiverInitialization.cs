using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MoDi.Desktop.Services;

internal sealed record ReceiverInitializationResult(string[] Ready, string[] Failed)
{
    public string Message => Failed.Length == 0 ? "就绪：等待手机选择链路并连接"
        : Ready.Length == 0 ? "接收服务未就绪：" + string.Join("、", Failed)
        : "部分链路可用；未就绪：" + string.Join("、", Failed);
}

internal sealed class ReceiverInitialization
{
    private readonly HashSet<string> _ready = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ReceiverInitializationResult> RunAsync(IEnumerable<(string Name, Func<Task<bool>> Start)> links)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var failed = new List<string>();
            foreach (var (name, start) in links)
            {
                if (_ready.Contains(name)) continue;
                try
                {
                    if (await start().ConfigureAwait(false)) { _ready.Add(name); continue; }
                }
                catch (Exception) { /* A failed optional transport must not suppress the others. */ }
                failed.Add(name);
            }
            return new(_ready.ToArray(), failed.ToArray());
        }
        finally { _gate.Release(); }
    }
}
