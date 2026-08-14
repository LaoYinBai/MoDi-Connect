using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.About;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.About;

[Collection("Avalonia UI")]
public sealed class AboutPageViewTests
{
    [Fact]
    public void About_page_composes_three_content_cards_actions_documents_and_fishing_dock()
    {
        TestApplicationHost.Ensure();
        using var vm = CreateAbout();
        var page = new AboutPage { DataContext = vm };
        var window = new Window { Width = 960, Height = 760, Content = page };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Single(page.GetLogicalDescendants().OfType<StoryCard>());
            Assert.Single(page.GetLogicalDescendants().OfType<SupportCard>());
            Assert.Single(page.GetLogicalDescendants().OfType<SponsorCard>());
            Assert.Single(page.GetLogicalDescendants().OfType<DocumentDialogView>());
            Assert.Single(page.GetLogicalDescendants().OfType<FishingDockView>());
            var dock = Assert.Single(page.GetLogicalDescendants().OfType<Image>(), image => image.Name == "FishingDockImage");
            var bitmap = Assert.IsType<Bitmap>(dock.Source);
            Assert.True(bitmap.PixelSize.Width > 0);
            Assert.True(bitmap.PixelSize.Height > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void About_actions_are_bound_to_the_page_view_model()
    {
        TestApplicationHost.Ensure();
        using var vm = CreateAbout();
        var page = new AboutPage { DataContext = vm };
        var window = new Window { Content = page };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(vm.ContactCommand, page.FindControl<Button>("ContactButton")?.Command);
            Assert.Same(vm.ExportLogsCommand, page.FindControl<Button>("AboutLogsButton")?.Command);
            Assert.Same(vm.CopyInfoCommand, page.FindControl<Button>("CopyInfoButton")?.Command);
            Assert.Same(vm.ShowReleaseNotesCommand, page.FindControl<Button>("ReleaseNotesButton")?.Command);
            Assert.Same(vm.ShowThirdPartyNoticesCommand, page.FindControl<Button>("ThirdPartyNoticesButton")?.Command);
        }
        finally
        {
            window.Close();
        }
    }

    private static AboutPageViewModel CreateAbout()
    {
        var provider = new RecordingMarkdownContentProvider();
        var navigation = new RecordingExternalNavigationService();
        return new AboutPageViewModel(
            new StoryCardViewModel(provider, MarkdownContentKey.Stories),
            new SupportCardViewModel(provider, MarkdownContentKey.TechnicalSupport, navigation),
            new SponsorCardViewModel(provider, MarkdownContentKey.Sponsors, navigation),
            new MarkdownDocumentViewModel(provider, MarkdownContentKey.ReleaseNotes),
            new MarkdownDocumentViewModel(provider, MarkdownContentKey.ThirdPartyNotices),
            navigation,
            new RecordingClipboardService(),
            new RecordingLogExportService(),
            "1.0.0");
    }
}
