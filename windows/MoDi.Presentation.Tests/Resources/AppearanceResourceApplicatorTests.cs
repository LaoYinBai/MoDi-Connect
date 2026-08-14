using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using MoDi.App.Contracts;
using MoDi.Presentation.Theming;

namespace MoDi.Presentation.Tests.Resources;

[Collection("Avalonia UI")]
public sealed class AppearanceResourceApplicatorTests
{
    [Fact]
    public void Paper_day_selects_the_light_theme_variant()
    {
        var application = TestApplicationHost.Ensure();

        AppearanceResourceApplicator.Apply(
            application,
            AppearanceSnapshot.Default with { Preset = ThemePreset.PaperDay });

        Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
    }

    [Fact]
    public void Custom_palette_replaces_all_eight_semantic_roles()
    {
        var application = TestApplicationHost.Ensure();
        var palette = new CustomPalette(
            "#101112", "#202122", "#303132", "#E0E1E2", "#A0A1A2", "#F06040", "#505152", "#40A060");

        try
        {
            AppearanceResourceApplicator.Apply(
                application,
                AppearanceSnapshot.Default with { Preset = ThemePreset.Custom, Palette = palette });

            AssertColor(application, "SurfaceBg", "#ff101112");
            AssertColor(application, "SurfaceCard", "#ff202122");
            AssertColor(application, "SurfaceCardSecondary", "#ff303132");
            AssertColor(application, "TextPrimary", "#ffe0e1e2");
            AssertColor(application, "TextSecondary", "#ffa0a1a2");
            AssertColor(application, "AccentPrimary", "#fff06040");
            AssertColor(application, "BorderDefault", "#ff505152");
            AssertColor(application, "Success", "#ff40a060");
        }
        finally
        {
            AppearanceResourceApplicator.Apply(application, AppearanceSnapshot.Default);
        }
    }

    private static void AssertColor(Application application, string key, string expected)
    {
        Assert.True(application.TryFindResource(key, out var value));
        var brush = Assert.IsType<SolidColorBrush>(value);
        Assert.Equal(expected, brush.Color.ToString(), ignoreCase: true);
    }
}
