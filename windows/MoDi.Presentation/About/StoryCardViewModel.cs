using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.About;

public sealed class StoryCardViewModel : ObservableObject, IDisposable
{
    public StoryCardViewModel(IMarkdownContentProvider provider, MarkdownContentKey key)
    {
        Title = "故事汇";
        Content = new MarkdownDocumentViewModel(provider, key);
    }

    public string Title { get; }
    public MarkdownDocumentViewModel Content { get; }
    public void Dispose() => Content.Dispose();
}
