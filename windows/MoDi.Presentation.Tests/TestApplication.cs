using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace MoDi.Presentation.Tests;

internal sealed class TestApplication : Application
{
    private bool _presentationResourcesLoaded;

    public void LoadPresentationResources()
    {
        if (_presentationResourcesLoaded)
            return;

        _presentationResourcesLoaded = true;
        Styles.Add(new FluentTheme());
        Resources.MergedDictionaries.Add(new ResourceInclude(
            new Uri("avares://MoDi.Presentation.Tests/TestApplication"))
        {
            Source = new Uri("avares://MoDi.Presentation/Styles/PresentationResources.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://MoDi.Presentation.Tests/TestApplication"))
        {
            Source = new Uri("avares://MoDi.Presentation/Styles/PresentationStyles.axaml"),
        });
    }
}

internal static class TestApplicationHost
{
    public static TestApplication Ensure()
    {
        if (Application.Current is null)
        {
            AppBuilder.Configure<TestApplication>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }

        var application = Assert.IsType<TestApplication>(Application.Current);
        application.LoadPresentationResources();
        return application;
    }
}

[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
public sealed class AvaloniaUiCollection
{
}
