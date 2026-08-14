namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path) => Path = path;

    public string Path { get; }

    public static TempDirectory Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "modi-desktop-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
