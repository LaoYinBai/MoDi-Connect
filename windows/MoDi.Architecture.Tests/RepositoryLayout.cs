namespace MoDi.Architecture.Tests;

internal static class RepositoryLayout
{
    public static string Root { get; } = FindRoot();

    public static string Resolve(string relativePath) =>
        Path.GetFullPath(relativePath.Replace('/', Path.DirectorySeparatorChar), Root);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitMarker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }
}
