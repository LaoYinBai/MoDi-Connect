using MoDi.Desktop.Platform.Appearance;

namespace MoDi.Desktop.Tests.TestDoubles;

internal static class DesktopTestFactory
{
    public static AppearanceService CreateAppearanceService(string root, TimeProvider? timeProvider = null) =>
        new(
            new ApplicationDataPaths(root),
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero)));
}
