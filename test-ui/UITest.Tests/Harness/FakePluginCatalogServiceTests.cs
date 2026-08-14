using MoDi.App.Contracts;
using UITest.Fakes;
using Xunit;

namespace UITest.Tests.Harness;

public sealed class FakePluginCatalogServiceTests
{
    [Theory]
    [InlineData("healthy", PluginHealth.Healthy)]
    [InlineData("disabled", PluginHealth.Disabled)]
    [InlineData("incompatible", PluginHealth.Incompatible)]
    [InlineData("crashed", PluginHealth.Crashed)]
    [InlineData("loading", PluginHealth.Loading)]
    public void Scenario_exposes_the_exact_requested_health(string scenario, PluginHealth expected)
    {
        var catalog = new FakePluginCatalogService();

        catalog.SetScenario(scenario);

        Assert.Equal(expected, Assert.Single(catalog.Snapshot.Entries).Health);
    }

    [Fact]
    public void Initial_catalog_keeps_the_realistic_built_in_audio_entry()
    {
        var catalog = new FakePluginCatalogService();

        var entry = Assert.Single(catalog.Snapshot.Entries);
        Assert.True(entry.IsBuiltIn);
        Assert.Equal(PluginHealth.BuiltIn, entry.Health);
    }
}
