using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.About;

public sealed class AboutPageViewModelTests
{
    [Fact]
    public async Task Five_internal_documents_use_the_exact_content_keys()
    {
        var provider = new RecordingMarkdownContentProvider();
        var navigation = new RecordingExternalNavigationService();
        using var vm = CreateAbout(provider, navigation);

        await vm.Story.Content.LoadCommand.ExecuteAsync();
        await vm.Support.Content.LoadCommand.ExecuteAsync();
        await vm.Sponsor.Content.LoadCommand.ExecuteAsync();
        await vm.ReleaseNotes.LoadCommand.ExecuteAsync();
        await vm.ThirdPartyNotices.LoadCommand.ExecuteAsync();

        Assert.Equal(
        [
            MarkdownContentKey.Stories,
            MarkdownContentKey.TechnicalSupport,
            MarkdownContentKey.Sponsors,
            MarkdownContentKey.ReleaseNotes,
            MarkdownContentKey.ThirdPartyNotices
        ], provider.RequestedKeys);
    }

    [Fact]
    public async Task Contact_logs_and_copy_actions_delegate_to_their_own_contracts()
    {
        var navigation = new RecordingExternalNavigationService();
        var clipboard = new RecordingClipboardService();
        var logs = new RecordingLogExportService();
        using var vm = CreateAbout(
            new RecordingMarkdownContentProvider(), navigation, clipboard, logs, version: "1.2.3");

        await vm.ContactCommand.ExecuteAsync();
        Assert.Equal(ExternalDestination.TechnicalSupport, navigation.LastDestination);

        await vm.ExportLogsCommand.ExecuteAsync();
        Assert.Equal("已导出：MoDi-test-logs.zip", vm.FeedbackText);

        await vm.CopyInfoCommand.ExecuteAsync();

        Assert.Equal(1, logs.ExportCalls);
        Assert.Equal(1, clipboard.CopyCalls);
        Assert.Equal(
            "墨堤\n版本 1.2.3\n作者：Silvite\n开源许可：GNU GPL v3\n霞鹜文楷：SIL Open Font License 1.1",
            clipboard.LastText);
        Assert.Equal("关于信息已复制", vm.FeedbackText);
    }

    [Fact]
    public void Release_notes_and_notices_are_both_reachable_through_document_commands()
    {
        using var vm = CreateAbout();

        vm.ShowReleaseNotesCommand.Execute(null);
        Assert.Same(vm.ReleaseNotes, vm.ActiveDocument);
        Assert.True(vm.IsDocumentDialogOpen);

        vm.CloseDocumentCommand.Execute(null);
        vm.ShowThirdPartyNoticesCommand.Execute(null);
        Assert.Same(vm.ThirdPartyNotices, vm.ActiveDocument);
        Assert.True(vm.IsDocumentDialogOpen);
    }

    [Fact]
    public async Task Support_and_sponsor_actions_use_typed_destinations()
    {
        var navigation = new RecordingExternalNavigationService();
        using var vm = CreateAbout(navigation: navigation);

        await vm.Support.OpenSupportCommand.ExecuteAsync();
        Assert.Equal(ExternalDestination.TechnicalSupport, navigation.LastDestination);

        await vm.Sponsor.OpenSponsorCommand.ExecuteAsync();
        Assert.Equal(ExternalDestination.SponsorPage, navigation.LastDestination);
        Assert.Equal(2, navigation.OpenCalls);
    }

    private static AboutPageViewModel CreateAbout(
        RecordingMarkdownContentProvider? provider = null,
        RecordingExternalNavigationService? navigation = null,
        RecordingClipboardService? clipboard = null,
        RecordingLogExportService? logs = null,
        string version = "1.0.0")
    {
        provider ??= new RecordingMarkdownContentProvider();
        navigation ??= new RecordingExternalNavigationService();
        return new AboutPageViewModel(
            new StoryCardViewModel(provider, MarkdownContentKey.Stories),
            new SupportCardViewModel(provider, MarkdownContentKey.TechnicalSupport, navigation),
            new SponsorCardViewModel(provider, MarkdownContentKey.Sponsors, navigation),
            new MarkdownDocumentViewModel(provider, MarkdownContentKey.ReleaseNotes),
            new MarkdownDocumentViewModel(provider, MarkdownContentKey.ThirdPartyNotices),
            navigation,
            clipboard ?? new RecordingClipboardService(),
            logs ?? new RecordingLogExportService(),
            version);
    }
}
