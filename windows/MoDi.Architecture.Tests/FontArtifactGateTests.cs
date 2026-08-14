using System.Text.Json;

namespace MoDi.Architecture.Tests;

public sealed class FontArtifactGateTests
{
    [Fact]
    public void Application_hosts_do_not_register_an_unapproved_default_font()
    {
        foreach (var program in new[]
        {
            "windows/MoDi.Desktop/Program.cs",
            "test-ui/UITest/Program.cs",
        })
        {
            var source = File.ReadAllText(RepositoryLayout.Resolve(program));
            Assert.DoesNotContain("WithInterFont", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Normal_builds_fail_closed_on_the_shared_font_artifact_lock()
    {
        var artifactLockPath = RepositoryLayout.Resolve("assets/fonts/font-artifacts.lock.json");
        Assert.True(File.Exists(artifactLockPath));
        using var document = JsonDocument.Parse(File.ReadAllBytes(artifactLockPath));
        Assert.Equal(5, document.RootElement.GetProperty("artifacts").GetArrayLength());

        var androidBuild = File.ReadAllText(RepositoryLayout.Resolve("android/app/build.gradle.kts"));
        Assert.Contains("verifyFontArtifacts", androidBuild, StringComparison.Ordinal);
        Assert.Contains("scripts/fonts/verify_fonts.py", androidBuild, StringComparison.Ordinal);
        Assert.Contains("dependsOn(verifyFontArtifacts)", androidBuild, StringComparison.Ordinal);

        var presentationProject = File.ReadAllText(
            RepositoryLayout.Resolve("windows/MoDi.Presentation/MoDi.Presentation.csproj"));
        Assert.Contains("VerifyFontArtifacts", presentationProject, StringComparison.Ordinal);
        Assert.Contains("BeforeTargets=\"CoreCompile\"", presentationProject, StringComparison.Ordinal);
        Assert.Contains("scripts\\fonts\\verify_fonts.py", presentationProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Packaged_font_notices_match_the_five_current_design_roles()
    {
        string[] expectedLicenseFiles =
        [
            "alimama_dongfang_dakai_license.txt",
            "lxgw_wenkai_ofl.txt",
            "zhuque_fangsong_ofl.txt",
            "genyo_mincho_ofl.txt",
            "source_han_serif_ofl.txt",
        ];
        var licenseRoot = RepositoryLayout.Resolve("assets/fonts/android-res/raw");
        Assert.Equal(
            expectedLicenseFiles.Order(StringComparer.Ordinal),
            Directory.GetFiles(licenseRoot).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        var notice = File.ReadAllText(
            RepositoryLayout.Resolve("windows/MoDi.Desktop/Content/ThirdPartyNotices.md"));
        foreach (var name in new[] { "阿里妈妈东方大楷", "霞鹜文楷", "朱雀仿宋", "源樣明體", "思源宋体" })
            Assert.Contains(name, notice, StringComparison.Ordinal);

        Assert.DoesNotContain("霞鹜文楷 Lite", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("FandolFang", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("Noto Sans SC", notice, StringComparison.Ordinal);

        var attributes = File.ReadAllText(RepositoryLayout.Resolve(".gitattributes"));
        Assert.Contains("assets/fonts/android-res/raw/** -text", attributes, StringComparison.Ordinal);
        Assert.Contains("LICENSES/** -text", attributes, StringComparison.Ordinal);
        Assert.Contains("android/app/libs/*.LICENSE.txt -text", attributes, StringComparison.Ordinal);
    }
}
