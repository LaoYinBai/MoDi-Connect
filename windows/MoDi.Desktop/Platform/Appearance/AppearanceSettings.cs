using System.Text.Json;
using System.Text.Json.Serialization;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Appearance;

internal sealed record AppearanceSettingsV1(
    int SchemaVersion,
    ThemePreset Preset,
    CustomPalette Palette,
    string? BackgroundFileName,
    bool ReduceMotion,
    double FeatureRailWidth)
{
    public const int CurrentSchemaVersion = 1;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static AppearanceSettingsV1 Default { get; } = FromSnapshot(AppearanceSnapshot.Default);

    public static AppearanceSettingsV1 FromSnapshot(AppearanceSnapshot snapshot) => new(
        CurrentSchemaVersion,
        snapshot.Preset,
        snapshot.Palette,
        snapshot.BackgroundDisplayName,
        snapshot.ReduceMotion,
        snapshot.FeatureRailWidth);

    public AppearanceSnapshot ToSnapshot() => new(
        Preset,
        Palette,
        BackgroundFileName,
        ReduceMotion,
        FeatureRailWidth);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
