using System.Text.Json;
using MoDi.Desktop.Platform.Storage;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class AtomicJsonStoreTests
{
    [Fact]
    public async Task Write_replaces_target_and_removes_same_directory_temp_file()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "appearance", "settings.v1.json");
        var store = new AtomicJsonStore<Payload>(path, TimeProvider.System);

        await store.WriteAsync(new Payload(2, "paper"), CancellationToken.None);

        var value = JsonSerializer.Deserialize<Payload>(await File.ReadAllTextAsync(path));
        Assert.Equal(new Payload(2, "paper"), value);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Corrupt_json_is_quarantined_without_touching_sibling_files()
    {
        using var temp = TempDirectory.Create();
        var directory = Path.Combine(temp.Path, "appearance");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.v1.json");
        var sibling = Path.Combine(temp.Path, "startup.json");
        await File.WriteAllTextAsync(path, "{broken");
        await File.WriteAllTextAsync(sibling, "keep");
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 1, 2, 3, 456, TimeSpan.Zero));
        var store = new AtomicJsonStore<Payload>(path, clock);

        var loaded = await store.ReadAsync(CancellationToken.None);

        Assert.Null(loaded);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(directory, "settings.v1.corrupt-20260811T010203456Z.json")));
        Assert.Equal("keep", await File.ReadAllTextAsync(sibling));
    }

    private sealed record Payload(int Version, string Name);
}
