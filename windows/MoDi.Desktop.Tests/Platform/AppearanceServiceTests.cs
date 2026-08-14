using System.Text.Json;
using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Appearance;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class AppearanceServiceTests
{
    [Fact]
    public async Task Appearance_write_replaces_the_versioned_file_atomically()
    {
        using var temp = TempDirectory.Create();
        var service = DesktopTestFactory.CreateAppearanceService(temp.Path);

        var result = await service.SelectPresetAsync(ThemePreset.PaperDay, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var path = Path.Combine(temp.Path, "appearance", "settings.v1.json");
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"preset\": \"PaperDay\"", json);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults_and_preserve_sibling_service_data()
    {
        using var temp = TempDirectory.Create();
        var appearanceDirectory = Path.Combine(temp.Path, "appearance");
        Directory.CreateDirectory(appearanceDirectory);
        File.WriteAllText(Path.Combine(appearanceDirectory, "settings.v1.json"), "not-json");
        File.WriteAllText(Path.Combine(temp.Path, "paired.json"), "keep-pairing");

        var service = DesktopTestFactory.CreateAppearanceService(temp.Path);

        Assert.Equal(AppearanceSnapshot.Default, service.Snapshot);
        Assert.Equal("keep-pairing", File.ReadAllText(Path.Combine(temp.Path, "paired.json")));
        Assert.Single(Directory.GetFiles(appearanceDirectory, "settings.v1.corrupt-*.json"));
    }

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task Imported_png_or_jpeg_is_validated_and_copied_to_owned_background(
        string displayName,
        byte[] bytes,
        string extension)
    {
        using var temp = TempDirectory.Create();
        var service = DesktopTestFactory.CreateAppearanceService(temp.Path);

        var result = await service.ImportBackgroundAsync(
            new SelectedImage(displayName, bytes),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(
            Path.Combine(temp.Path, "appearance", "background" + extension)));
        Assert.Equal("background" + extension, service.Snapshot.BackgroundDisplayName);
    }

    [Fact]
    public async Task Invalid_image_signature_is_rejected_without_copying()
    {
        using var temp = TempDirectory.Create();
        var service = DesktopTestFactory.CreateAppearanceService(temp.Path);

        var result = await service.ImportBackgroundAsync(
            new SelectedImage("fake.png", new byte[] { 1, 2, 3, 4 }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("APPEARANCE_IMAGE_FORMAT", result.ErrorCode);
        Assert.Empty(Directory.Exists(Path.Combine(temp.Path, "appearance"))
            ? Directory.GetFiles(Path.Combine(temp.Path, "appearance"), "background.*")
            : []);
    }

    [Theory]
    [InlineData(56, 56)]
    [InlineData(100, 56)]
    [InlineData(129, 200)]
    [InlineData(200, 200)]
    public async Task Rail_width_persists_only_compact_or_expanded(double requested, double expected)
    {
        using var temp = TempDirectory.Create();
        var service = DesktopTestFactory.CreateAppearanceService(temp.Path);

        await service.SetFeatureRailWidthAsync(requested, CancellationToken.None);
        var reloaded = DesktopTestFactory.CreateAppearanceService(temp.Path);

        Assert.Equal(expected, service.Snapshot.FeatureRailWidth);
        Assert.Equal(expected, reloaded.Snapshot.FeatureRailWidth);
    }

    public static TheoryData<string, byte[], string> ValidImages => new()
    {
        { "paper.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1], ".png" },
        { "paper.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1], ".jpg" },
    };
}
