using System.Text.RegularExpressions;

namespace MoDi.Architecture.Tests;

public sealed class PresentationBindingBoundaryTests
{
    private static readonly string[] AllowedLocalElementBindings =
    [
        "P2p/PairedDevicesOverlay.axaml|PairedDevicesRoot",
        "Settings/PluginManagerCard.axaml|PluginRoot",
        "Settings/PluginManagerCard.axaml|PluginRoot",
        "Settings/ThemeCard.axaml|ThemeRoot",
    ];

    [Fact]
    public void Element_name_bindings_are_limited_to_documented_same_module_roots()
    {
        var root = RepositoryLayout.Resolve("windows/MoDi.Presentation");
        var actual = Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"ElementName=([A-Za-z0-9_]+)")
                .Select(match => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}|{match.Groups[1].Value}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedLocalElementBindings.OrderBy(value => value, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void Presentation_has_no_ancestor_binding_or_service_locator()
    {
        var root = RepositoryLayout.Resolve("windows/MoDi.Presentation");
        var source = string.Join('\n', Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

        Assert.DoesNotContain("RelativeSource", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceLocator", source, StringComparison.Ordinal);
    }
}
