using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.Markdown;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Markdown;

[Collection("Avalonia UI")]
public sealed class MarkdownDocumentViewTests
{
    [Fact]
    public async Task Closed_document_model_renders_without_arbitrary_content()
    {
        TestApplicationHost.Ensure();
        var provider = new RecordingMarkdownContentProvider();
        provider.Set(MarkdownContentKey.Stories,
            "# 标题\n\n正文 **强调**。\n\n- 条目\n\n> 引用\n\n~~~text\ncode\n~~~");
        using var vm = new MarkdownDocumentViewModel(provider, MarkdownContentKey.Stories);
        await vm.LoadCommand.ExecuteAsync();
        var view = new MarkdownDocumentView { DataContext = vm };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var text = view.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
            Assert.Contains("标题", text);
            Assert.Contains("•", text);
            Assert.Contains("code", text);
            Assert.Contains("MoDi UI Body Zhuque Fangsong",
                Assert.Single(view.GetLogicalDescendants().OfType<SelectableTextBlock>()).FontFamily.Name,
                StringComparison.Ordinal);
            Assert.DoesNotContain(view.GetLogicalDescendants(), control => control.GetType().Name.Contains("WebView"));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public async Task Safe_link_click_is_forwarded_without_opening_it()
    {
        TestApplicationHost.Ensure();
        var provider = new RecordingMarkdownContentProvider();
        provider.Set(MarkdownContentKey.Stories, "访问 [项目主页](https://example.com/project)。");
        using var vm = new MarkdownDocumentViewModel(provider, MarkdownContentKey.Stories);
        await vm.LoadCommand.ExecuteAsync();
        var view = new MarkdownDocumentView { DataContext = vm };
        Uri? requested = null;
        view.SafeLinkRequested += (_, args) => requested = args.Uri;
        var window = new Window { Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var link = Assert.Single(view.GetLogicalDescendants().OfType<Button>(), button => button.Tag is Uri);

            link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(new Uri("https://example.com/project"), requested);
        }
        finally
        {
            window.Close();
        }
    }
}
