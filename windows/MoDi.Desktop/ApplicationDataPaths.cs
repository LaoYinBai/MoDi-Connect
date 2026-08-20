using System;
using System.IO;

namespace MoDi.Desktop;

public sealed class ApplicationDataPaths
{
    public ApplicationDataPaths(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("应用数据根目录不能为空", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
    public string AppearanceDirectory => Path.Combine(RootDirectory, "appearance");
    public string AppearanceSettingsFile => Path.Combine(AppearanceDirectory, "settings.v1.json");
    public string OnboardingSettingsFile => Path.Combine(RootDirectory, "onboarding.v1.json");
    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
    public string ExportsDirectory => Path.Combine(RootDirectory, "exports");

    public static ApplicationDataPaths CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoDi"));
}
