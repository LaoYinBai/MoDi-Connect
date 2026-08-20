using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Appearance;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class PersonalizationResetServiceTests
{
    [Fact]
    public async Task Reset_changes_only_the_appearance_target()
    {
        var appearance = new RecordingAppearanceResetTarget();
        var service = new PersonalizationResetService(appearance);

        var result = await service.ResetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppearanceSnapshot.Default, appearance.LastReset);
        Assert.Equal(1, appearance.ResetCalls);
    }

    [Fact]
    public async Task Real_reset_removes_only_owned_background_and_restores_defaults()
    {
        using var temp = TempDirectory.Create();
        var appearance = await DesktopTestFactory.CreateAppearanceServiceAsync(temp.Path);
        await appearance.ImportBackgroundAsync(
            new SelectedImage("paper.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            CancellationToken.None);
        var unrelated = Path.Combine(temp.Path, "paired.json");
        await File.WriteAllTextAsync(unrelated, "keep");

        var result = await new PersonalizationResetService(appearance).ResetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppearanceSnapshot.Default, appearance.Snapshot);
        Assert.False(File.Exists(Path.Combine(temp.Path, "appearance", "background.png")));
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelated));
    }
}
