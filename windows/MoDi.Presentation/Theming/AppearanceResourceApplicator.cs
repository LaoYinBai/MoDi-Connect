using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using MoDi.App.Contracts;

namespace MoDi.Presentation.Theming;

public static class AppearanceResourceApplicator
{
    private static readonly string[] CustomResourceKeys =
    [
        "SurfaceBg",
        "SurfaceCard",
        "SurfaceCardSecondary",
        "TextPrimary",
        "TextSecondary",
        "TextMuted",
        "AccentPrimary",
        "AccentBg",
        "BorderDefault",
        "MeterTrack",
        "StagePaperBrush",
        "StageInkBrush",
        "Success",
    ];

    public static void Apply(Application application, AppearanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Preset == ThemePreset.Custom)
        {
            application.RequestedThemeVariant = ThemeVariant.Dark;
            ApplyCustomPalette(application, snapshot.Palette);
        }
        else
        {
            RemoveCustomPalette(application);
            application.RequestedThemeVariant = snapshot.Preset == ThemePreset.PaperDay
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }

        application.Resources["ReduceMotion"] = snapshot.ReduceMotion;
        application.Resources["FeatureRailWidth"] = snapshot.FeatureRailWidth;
        application.Resources["BackgroundDisplayName"] = snapshot.BackgroundDisplayName ?? string.Empty;
    }

    private static void ApplyCustomPalette(Application application, CustomPalette palette)
    {
        SetBrush(application, "SurfaceBg", palette.Background);
        SetBrush(application, "SurfaceCard", palette.Surface);
        SetBrush(application, "SurfaceCardSecondary", palette.SurfaceElevated);
        SetBrush(application, "TextPrimary", palette.TextPrimary);
        SetBrush(application, "TextSecondary", palette.TextSecondary);
        SetBrush(application, "TextMuted", palette.TextSecondary);
        SetBrush(application, "AccentPrimary", palette.Accent);
        SetBrush(application, "AccentBg", palette.SurfaceElevated);
        SetBrush(application, "BorderDefault", palette.Border);
        SetBrush(application, "MeterTrack", palette.Border);
        SetBrush(application, "StagePaperBrush", palette.Background);
        SetBrush(application, "StageInkBrush", palette.TextPrimary);
        SetBrush(application, "Success", palette.Success);
    }

    private static void RemoveCustomPalette(Application application)
    {
        foreach (var key in CustomResourceKeys)
            application.Resources.Remove(key);
    }

    private static void SetBrush(Application application, string key, string color) =>
        application.Resources[key] = new SolidColorBrush(Color.Parse(color));
}
