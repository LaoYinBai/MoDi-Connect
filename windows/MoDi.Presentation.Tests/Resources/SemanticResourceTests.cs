using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace MoDi.Presentation.Tests.Resources;

[Collection("Avalonia UI")]
public sealed class SemanticResourceTests
{
    [Theory]
    [InlineData(true, "#FF151A1D", "#FFF2EFE6")]
    [InlineData(false, "#FFF4F1E7", "#FF242A2D")]
    public void Theme_variants_resolve_the_accepted_surface_and_text_colors(
        bool dark,
        string expectedSurface,
        string expectedText)
    {
        var application = TestApplicationHost.Ensure();
        var theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        AssertBrush(application, "SurfaceBg", expectedSurface, theme);
        AssertBrush(application, "TextPrimary", expectedText, theme);
    }

    [Fact]
    public void Shared_accent_colors_keep_the_accepted_values()
    {
        var application = TestApplicationHost.Ensure();

        AssertBrush(application, "AccentPrimary", "#FFE8863C");
        AssertBrush(application, "Success", "#FF47937F");
        AssertBrush(application, "Error", "#FFC2452D");
    }

    private static void AssertBrush(
        Application application,
        string key,
        string expected,
        ThemeVariant? theme = null)
    {
        Assert.True(
            application.Resources.TryGetResource(key, theme, out var resource),
            $"Missing semantic resource: {key}");
        var brush = Assert.IsType<SolidColorBrush>(resource);
        Assert.True(
            string.Equals(expected, brush.Color.ToString(), StringComparison.OrdinalIgnoreCase),
            $"Expected {key} to be {expected}, got {brush.Color}.");
    }
}
