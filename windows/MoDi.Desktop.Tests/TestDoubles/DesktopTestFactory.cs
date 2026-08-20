using MoDi.Desktop.Platform.Appearance;

namespace MoDi.Desktop.Tests.TestDoubles;

internal static class DesktopTestFactory
{
    public static Task<AppearanceService> CreateAppearanceServiceAsync(
        string root,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default) =>
        AppearanceService.CreateAsync(
            new ApplicationDataPaths(root),
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero)),
            cancellationToken);

}
