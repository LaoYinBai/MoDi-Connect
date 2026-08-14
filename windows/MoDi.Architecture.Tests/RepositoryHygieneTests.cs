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

    [Fact]
    public void Canonical_Chinese_project_documents_exist()
    {
        string[] requiredDocuments =
        [
            "docs/README.md",
            "docs/当前进度.md",
            "docs/开发路线图.md",
            "docs/架构/项目结构.md",
            "docs/发布/发布总检查清单.md",
        ];

        var missingDocuments = requiredDocuments
            .Where(path => !File.Exists(RepositoryLayout.Resolve(path)))
            .ToArray();

        Assert.Empty(missingDocuments);
    }

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

    [Fact]
    public void Superpowers_active_directories_contain_only_current_work()
    {
        var expectedByDirectory = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["docs/superpowers/plans"] =
            [
                "2026-08-13-android-lan-device-panel-implementation.md",
                "2026-08-11-protocol-binary-boundary-package-b.md",
                "2026-08-13-cross-platform-font-library-implementation.md",
                "2026-08-14-protocol-proprietary-closeout.md",
            ],
            ["docs/superpowers/specs"] =
            [
                "2026-08-13-android-lan-device-panel-design.md",
                "2026-08-13-cross-platform-font-library-design.md",
                "2026-08-14-protocol-proprietary-closeout-design.md",
            ],
            ["docs/superpowers/checkpoints"] =
            [
                "2026-08-13-protocol-binary-boundary-package-b.md",
            ],
        };

        foreach (var (directory, expectedFiles) in expectedByDirectory)
        {
            var actualFiles = EnumerateFilesIfPresent(directory, "*.md")
                .Select(Path.GetFileName)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(
                expectedFiles.Order(StringComparer.OrdinalIgnoreCase),
                actualFiles,
                StringComparer.OrdinalIgnoreCase);
        }
    }

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
