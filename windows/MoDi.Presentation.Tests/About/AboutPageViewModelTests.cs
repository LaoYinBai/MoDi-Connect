using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.About;

public sealed class AboutPageViewModelTests
{
    [Fact]
    public void Cards_expose_short_previews_and_open_browsable_content_libraries()
    {
        var provider = new RecordingMarkdownContentProvider();
        var navigation = new RecordingExternalNavigationService();
        using var vm = CreateAbout(provider, navigation);

        Assert.Contains("水墨", vm.Story.Preview);
        Assert.Contains("更新", vm.Support.Preview);
        Assert.Contains("赞助", vm.Sponsor.Preview);

        vm.Story.OpenLibraryCommand.Execute(null);
        Assert.True(vm.IsLibraryDialogOpen);
        Assert.Equal("故事汇", vm.ActiveLibrary?.Title);
        Assert.Equal(3, vm.ActiveLibrary?.Items.Count);

        vm.CloseLibraryCommand.Execute(null);
        vm.Support.OpenLibraryCommand.Execute(null);
        Assert.Equal("技术支持", vm.ActiveLibrary?.Title);
        Assert.Equal(3, vm.ActiveLibrary?.Items.Count);

        vm.CloseLibraryCommand.Execute(null);
        vm.Sponsor.OpenListCommand.Execute(null);
        Assert.Equal("赞助名单", vm.ActiveLibrary?.Title);
        Assert.Single(vm.ActiveLibrary?.Items ?? []);
    }

    [Fact]
    public async Task Selecting_a_library_item_loads_that_exact_embedded_document()
    {
        var provider = new RecordingMarkdownContentProvider();
        using var vm = CreateAbout(provider);

        vm.Story.OpenLibraryCommand.Execute(null);
        var target = vm.ActiveLibrary!.Items[1];
        await vm.ActiveLibrary.SelectCommand.ExecuteAsync(target);

        Assert.Same(target, vm.ActiveLibrary.SelectedItem);
        Assert.Equal(MarkdownContentKey.StoryCurrentChapter, provider.RequestedKeys.Last());
        Assert.True(target.Document.IsLoaded);
    }

    [Fact]
    public async Task Keyboard_or_pointer_list_selection_updates_the_visible_document()
    {
        var provider = new RecordingMarkdownContentProvider();
        using var vm = CreateAbout(provider);
        vm.Story.OpenLibraryCommand.Execute(null);
        var target = vm.Stories.Items[2];

        vm.Stories.SelectedItem = target;
        await Task.Yield();

        Assert.Same(target, vm.Stories.SelectedItem);
        Assert.True(target.Document.IsLoaded);
        Assert.Contains(MarkdownContentKey.StoryInkBridge, provider.RequestedKeys);
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
            provider,
            navigation,
            clipboard ?? new RecordingClipboardService(),
            logs ?? new RecordingLogExportService(),
            version);
    }
}
