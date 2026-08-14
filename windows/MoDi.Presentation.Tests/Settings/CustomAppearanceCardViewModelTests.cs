using MoDi.App.Contracts;
using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class CustomAppearanceCardViewModelTests
{
    [Fact]
    public async Task All_eight_custom_colors_accept_exact_rrggbb_values()
    {
        var appearance = new RecordingAppearanceService();
        var images = new RecordingImageSelectionService();
        using var vm = new CustomAppearanceCardViewModel(appearance, images)
        {
            Background = "#010203",
            Surface = "#111213",
            SurfaceElevated = "#212223",
            TextPrimary = "#F1F2F3",
            TextSecondary = "#A1A2A3",
            Accent = "#E8863C",
            Border = "#313233",
            Success = "#47937F"
        };

        await vm.SavePaletteCommand.ExecuteAsync();

        Assert.Equal(1, appearance.SavePaletteCalls);
        Assert.Equal(new CustomPalette(
            "#010203", "#111213", "#212223", "#F1F2F3",
            "#A1A2A3", "#E8863C", "#313233", "#47937F"), appearance.LastPalette);
        Assert.Null(vm.ErrorCode);
    }

    [Theory]
    [InlineData("151A1D")]
    [InlineData("#FFF")]
    [InlineData("#GG0000")]
    [InlineData("#151A1D00")]
    public async Task Invalid_color_is_rejected_without_changing_the_appearance_snapshot(string invalid)
    {
        var appearance = new RecordingAppearanceService();
        var original = appearance.Snapshot;
        using var vm = new CustomAppearanceCardViewModel(appearance, new RecordingImageSelectionService())
        {
            Background = invalid
        };

        await vm.SavePaletteCommand.ExecuteAsync();

        Assert.Equal("APPEARANCE_COLOR_INVALID", vm.ErrorCode);
        Assert.Equal(0, appearance.SavePaletteCalls);
        Assert.Same(original, appearance.Snapshot);
    }

    [Fact]
    public async Task Chosen_image_is_passed_as_bytes_instead_of_a_path()
    {
        var bytes = new byte[] { 1, 3, 5, 7 };
        var images = new RecordingImageSelectionService
        {
            Result = OperationResult<SelectedImage>.Success(new SelectedImage("river-bank.png", bytes))
        };
        var appearance = new RecordingAppearanceService();
        using var vm = new CustomAppearanceCardViewModel(appearance, images);

        await vm.SelectBackgroundCommand.ExecuteAsync();

        Assert.Equal(1, images.SelectCalls);
        Assert.Equal(1, appearance.ImportBackgroundCalls);
        Assert.Equal("river-bank.png", appearance.LastImportedImage?.DisplayName);
        Assert.Equal(bytes, appearance.LastImportedImage?.PngOrJpegBytes.ToArray());
        Assert.DoesNotContain(appearance.LastImportedImage!.GetType().GetProperties(),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Feature_rail_width_is_clamped_to_56_and_200()
    {
        var appearance = new RecordingAppearanceService();
        using var vm = new CustomAppearanceCardViewModel(appearance, new RecordingImageSelectionService());

        vm.FeatureRailWidth = 20;
        await vm.SaveRailWidthCommand.ExecuteAsync();
        Assert.Equal(56d, appearance.LastFeatureRailWidth);

        vm.FeatureRailWidth = 260;
        await vm.SaveRailWidthCommand.ExecuteAsync();
        Assert.Equal(200d, appearance.LastFeatureRailWidth);
    }
}
