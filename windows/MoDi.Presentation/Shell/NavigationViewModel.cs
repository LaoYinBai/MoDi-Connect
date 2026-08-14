using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Shell;

public sealed class NavigationViewModel : ObservableObject
{
    private AppPage _currentPage = AppPage.Main;

    public NavigationViewModel() => NavigateCommand = new RelayCommand<AppPage>(Navigate);

    public AppPage CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value))
                return;
            OnPropertyChanged(nameof(IsMainPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(IsAboutPage));
        }
    }

    public bool IsMainPage => CurrentPage == AppPage.Main;
    public bool IsSettingsPage => CurrentPage == AppPage.Settings;
    public bool IsAboutPage => CurrentPage == AppPage.About;
    public RelayCommand<AppPage> NavigateCommand { get; }

    private void Navigate(AppPage page)
    {
        if (Enum.IsDefined(page))
            CurrentPage = page;
    }
}
