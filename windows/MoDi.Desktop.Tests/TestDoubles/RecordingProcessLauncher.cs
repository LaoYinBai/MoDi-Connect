using MoDi.Desktop.Platform.Content;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class RecordingProcessLauncher : IProcessLauncher
{
    public string? LastTarget { get; private set; }
    public void Open(string target) => LastTarget = target;
}
