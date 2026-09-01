using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.About;

public sealed class StoryCardViewModel : ObservableObject
{
    public StoryCardViewModel(Action openLibrary) => OpenLibraryCommand = new RelayCommand(openLibrary);

    public string Title => "故事汇";
    public string LatestTitle => "为什么叫墨堤";
    public string Preview => "从名字的来处，到水墨桥的语法，记录墨堤如何让声音跨过设备。";
    public string CountText => "共 3 篇";
    public RelayCommand OpenLibraryCommand { get; }
}
