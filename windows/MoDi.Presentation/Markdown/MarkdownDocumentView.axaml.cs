using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace MoDi.Presentation.Markdown;

public sealed class SafeLinkRequestedEventArgs(Uri uri) : EventArgs
{
    public Uri Uri { get; } = uri;
}

public partial class MarkdownDocumentView : UserControl
{
    private MarkdownDocumentViewModel? _viewModel;

    public MarkdownDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        BindViewModel();
    }

    public event EventHandler<SafeLinkRequestedEventArgs>? SafeLinkRequested;

    private void OnDataContextChanged(object? sender, EventArgs eventArgs) => BindViewModel();

    private void BindViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MarkdownDocumentViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RebuildDocument();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MarkdownDocumentViewModel.Document))
            RebuildDocument();
    }

    private void RebuildDocument()
    {
        if (DocumentPanel is null)
            return;

        DocumentPanel.Children.Clear();
        if (_viewModel is null)
            return;

        foreach (var block in _viewModel.Document.Blocks)
            DocumentPanel.Children.Add(CreateBlock(block));
    }

    private Control CreateBlock(MarkdownBlock block) => block switch
    {
        HeadingBlock heading => CreateHeading(heading),
        ParagraphBlock paragraph => CreateText(paragraph.Inlines),
        ListBlock list => CreateList(list),
        QuoteBlock quote => CreateQuote(quote),
        CodeBlock code => CreateCode(code),
        _ => throw new InvalidOperationException("Unsupported closed Markdown block."),
    };

    private Control CreateHeading(HeadingBlock heading)
    {
        var text = CreateText(heading.Inlines);
        text.Classes.Add(heading.Level == 1 ? "page-title" : "section-title");
        return text;
    }

    private TextBlock CreateText(IReadOnlyList<MarkdownInline> inlines)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (inlines.All(inline => inline is TextInline))
        {
            text.Text = string.Concat(inlines.Cast<TextInline>().Select(inline => inline.Text));
            return text;
        }

        text.Inlines = [];
        AddInlines(text.Inlines, inlines);
        return text;
    }

    private Control CreateList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 };
        foreach (var item in list.Items)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = "•" });
            var content = CreateText(item.Inlines);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private Control CreateQuote(QuoteBlock quote)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 4),
            Child = CreateText(quote.Inlines),
        };
        if (TryResource("AccentPrimary", out var brush) && brush is IBrush typedBrush)
            border.BorderBrush = typedBrush;
        return border;
    }

    private Control CreateCode(CodeBlock code)
    {
        var text = new SelectableTextBlock
        {
            Text = code.Text,
            TextWrapping = TextWrapping.Wrap,
        };
        var border = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            Child = text,
        };
        if (TryResource("SurfaceCardSecondary", out var background) && background is IBrush typedBackground)
            border.Background = typedBackground;
        return border;
    }

    private void AddInlines(InlineCollection target, IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    target.Add(new Run(text.Text));
                    break;
                case InlineCode code:
                    target.Add(new Run($"`{code.Text}`"));
                    break;
                case StrongInline strong:
                {
                    var bold = new Bold();
                    AddInlines(bold.Inlines, strong.Inlines);
                    target.Add(bold);
                    break;
                }
                case EmphasisInline emphasis:
                {
                    var italic = new Italic();
                    AddInlines(italic.Inlines, emphasis.Inlines);
                    target.Add(italic);
                    break;
                }
                case LinkInline link:
                {
                    var button = new Button
                    {
                        Content = Flatten(link.Inlines),
                        Tag = link.Uri,
                        Padding = new Thickness(2, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    button.Click += OnSafeLinkClicked;
                    target.Add(button);
                    break;
                }
                default:
                    throw new InvalidOperationException("Unsupported closed Markdown inline.");
            }
        }
    }

    private void OnSafeLinkClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: Uri uri } && uri.Scheme == Uri.UriSchemeHttps)
            SafeLinkRequested?.Invoke(this, new SafeLinkRequestedEventArgs(uri));
    }

    private static bool TryResource(string key, out object? value)
    {
        value = null;
        return Application.Current?.TryFindResource(key, out value) == true;
    }

    private static string Flatten(IEnumerable<MarkdownInline> inlines) => string.Concat(inlines.Select(inline => inline switch
    {
        TextInline text => text.Text,
        InlineCode code => code.Text,
        StrongInline strong => Flatten(strong.Inlines),
        EmphasisInline emphasis => Flatten(emphasis.Inlines),
        LinkInline link => Flatten(link.Inlines),
        _ => string.Empty,
    }));
}
