using MoDi.Presentation.Infrastructure;
using MoDi.Presentation.Markdown;

namespace MoDi.Presentation.About;

public sealed record ContentLibraryItemViewModel(
    string Title,
    string Summary,
    MarkdownDocumentViewModel Document);

public sealed class ContentLibraryViewModel : ObservableObject, IDisposable
{
    private ContentLibraryItemViewModel? _selectedItem;

    public ContentLibraryViewModel(string title, IReadOnlyList<ContentLibraryItemViewModel> items)
    {
        if (items.Count == 0) throw new ArgumentException("内容列表不能为空", nameof(items));
        Title = title;
        Items = items;
        _selectedItem = items[0];
        SelectCommand = new AsyncRelayCommand<ContentLibraryItemViewModel>(SelectAsync, item => item is not null);
    }

    public string Title { get; }
    public IReadOnlyList<ContentLibraryItemViewModel> Items { get; }
    public ContentLibraryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null && !value.Document.IsLoaded)
                _ = value.Document.LoadCommand.ExecuteAsync();
        }
    }
    public AsyncRelayCommand<ContentLibraryItemViewModel> SelectCommand { get; }

    public Task LoadSelectedAsync(CancellationToken cancellationToken = default) =>
        SelectedItem is null ? Task.CompletedTask : SelectedItem.Document.LoadCommand.ExecuteAsync(cancellationToken);

    public void Dispose()
    {
        foreach (var item in Items) item.Document.Dispose();
        SelectCommand.RaiseCanExecuteChanged();
    }

    private async Task SelectAsync(ContentLibraryItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null) return;
        SelectedItem = item;
        if (!item.Document.IsLoaded)
            await item.Document.LoadCommand.ExecuteAsync(cancellationToken);
    }
}
