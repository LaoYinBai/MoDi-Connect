namespace MoDi.Architecture.Tests;

public sealed class RepositoryHygieneTests
{
    [Fact]
    public void Repository_root_contains_only_entry_and_infrastructure_files()
    {
        string[] allowedFiles =
        [
            ".git",
            ".gitattributes",
            ".gitignore",
            "LICENSE",
            "license-map.v1.json",
            "NuGet.config",
            "README.md",
            "version.json",
        ];

        var unexpectedFiles = Directory
            .EnumerateFiles(RepositoryLayout.Root, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null &&
                           !allowedFiles.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(unexpectedFiles);
    }

    // 社区仓不携带开发仓的中文进度/路线/发布清单等内部文档，
    // Canonical_Chinese_project_documents_exist 与
    // Superpowers_active_directories_contain_only_current_work
    // 两个开发仓文档清单断言已随发布裁剪移除。

    [Fact]
    public void TestUi_does_not_own_duplicate_presentation_resources()
    {
        var duplicateFiles = EnumerateFilesIfPresent("test-ui/UITest/Assets")
            .Concat(EnumerateFilesIfPresent("test-ui/UITest/Styles"))
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(duplicateFiles);
    }

    [Fact]
    public void Repository_does_not_ship_archived_VB_Cable_binaries()
    {
        var archivedBinaries = EnumerateFilesIfPresent("archive/libs/VB-Cable")
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(archivedBinaries);
    }

    [Fact]
    public void Driver_installer_points_to_vendor_instead_of_repository_binary()
    {
        var installer = File.ReadAllText(
            RepositoryLayout.Resolve("windows/scripts/install_driver.bat"));

        Assert.DoesNotContain("archive\\libs", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VBCABLE_Setup_x64.exe", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://vb-audio.com/Cable/", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void Android_main_source_set_excludes_debug_UI_prototypes()
    {
        string[] forbiddenMainSources =
        [
            "android/app/src/main/java/com/modi/connect/ui/TestUI.kt",
            "android/app/src/main/java/com/modi/connect/ui/QrScannerScreen.kt",
        ];

        var presentSources = forbiddenMainSources
            .Where(path => File.Exists(RepositoryLayout.Resolve(path)))
            .ToArray();

        Assert.Empty(presentSources);
    }

    // Superpowers 计划/规格/检查点目录属开发仓工作流，社区仓不携带（断言已移除）。

    private static IEnumerable<string> EnumerateFilesIfPresent(
        string relativeDirectory,
        string pattern = "*")
    {
        var directory = RepositoryLayout.Resolve(relativeDirectory);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            : [];
    }
}
