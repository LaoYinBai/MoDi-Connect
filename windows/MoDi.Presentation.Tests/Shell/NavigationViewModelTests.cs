using MoDi.Presentation.Shell;

namespace MoDi.Presentation.Tests.Shell;

public sealed class NavigationViewModelTests
{
    [Fact]
    public void Navigation_exposes_only_the_three_accepted_pages()
    {
        var viewModel = new NavigationViewModel();

        Assert.Equal(AppPage.Main, viewModel.CurrentPage);
        viewModel.NavigateCommand.Execute(AppPage.Settings);
        Assert.True(viewModel.IsSettingsPage);
        viewModel.NavigateCommand.Execute(AppPage.About);
        Assert.True(viewModel.IsAboutPage);
        Assert.False(viewModel.IsMainPage);
    }

    [Fact]
    public void Unknown_page_is_ignored()
    {
        var viewModel = new NavigationViewModel();

        viewModel.NavigateCommand.Execute((AppPage)99);

        Assert.Equal(AppPage.Main, viewModel.CurrentPage);
    }
}
