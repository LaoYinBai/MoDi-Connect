namespace MoDi.Architecture.Tests;

public sealed class ReceiverOrchestrationBoundaryTests
{
    [Fact]
    public void Receiver_controller_does_not_own_physical_links_or_subscribe_to_link_events()
    {
        var controller = File.ReadAllText(RepositoryLayout.Resolve(
            "windows/MoDi.Desktop/Services/ReceiverController.cs"));

        Assert.DoesNotContain("LinkManager", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("StartLanAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("StartBluetoothAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("StartUsbAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("OnStateChanged +=", controller, StringComparison.Ordinal);
        Assert.Contains("ReceiverLifecycleCoordinator", controller, StringComparison.Ordinal);
        Assert.Contains("ReceiverSnapshotProjection", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_composition_is_the_only_physical_link_composition_root()
    {
        var composition = File.ReadAllText(RepositoryLayout.Resolve(
            "windows/MoDi.Desktop/Composition/ProductionComposition.cs"));

        Assert.Contains(
            "new ReceiverController(new ReceiverLinkRuntime(new LinkManager()))",
            composition,
            StringComparison.Ordinal);
    }
}
