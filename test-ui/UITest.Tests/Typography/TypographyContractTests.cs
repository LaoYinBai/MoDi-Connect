using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MoDi.Presentation.Shell;
using UITest.Demo;
using Xunit;

namespace UITest.Tests.Typography;

[Collection("Avalonia UI")]
public sealed class TypographyContractTests
{
    [Fact]
    public void Host_uses_the_five_shared_design_book_families_and_sizes()
    {
        EnsureAvaloniaApplication();
        var application = Assert.IsType<App>(Application.Current);

        AssertFamily(application, "FontFamilyTitle", "Alimama DongFangDaKai");
        AssertFamily(application, "FontFamilyFunction", "MoDi UI Function LXGW WenKai");
        AssertFamily(application, "FontFamilyBody", "MoDi UI Body Zhuque Fangsong");
        AssertFamily(application, "FontFamilyAnnotation", "MoDi UI Annotation GenYo Mincho");
        AssertFamily(application, "FontFamilyDefault", "MoDi UI Default Source Han Serif");
        AssertSize(application, "FontSizeBrand", 24d);
        AssertSize(application, "FontSizePageTitle", 20d);
        AssertSize(application, "FontSizeSectionTitle", 16d);
        AssertSize(application, "FontSizePoetic", 15d);
        AssertSize(application, "FontSizeBody", 14d);
        AssertSize(application, "FontSizeCaption", 12d);
    }

    [Fact]
    public void Rendered_shared_shell_and_pages_use_semantic_typography()
    {
        EnsureAvaloniaApplication();
        var window = new MainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var composition = Assert.IsType<TestUiComposition>(window.DataContext);
            var topBar = Assert.Single(window.GetLogicalDescendants().OfType<TopBarView>());
            AssertTextRole(topBar.FindControl<TextBlock>("BrandText"), "Alimama DongFangDaKai", 24d);
            AssertControlRole(topBar.FindControl<ToggleButton>("MainNavigationButton"), "MoDi UI Function LXGW WenKai", 14d);
            AssertTextRole(FindText(window, "声音正在过桥", 20), "Alimama DongFangDaKai", 20d);

            composition.Shell.Navigation.NavigateCommand.Execute(AppPage.Settings);
            Dispatcher.UIThread.RunJobs();
            AssertTextRole(FindText(window, "设置", 20), "Alimama DongFangDaKai", 20d);

            composition.Shell.Navigation.NavigateCommand.Execute(AppPage.About);
            Dispatcher.UIThread.RunJobs();
            AssertTextRole(FindText(window, "关于墨堤", 20), "Alimama DongFangDaKai", 20d);
            AssertTextRole(
                FindText(window, "墨堤是一座水墨的桥。声音从桥上过，小男孩在桥头等。", 15),
                "MoDi UI Body Zhuque Fangsong",
                15d);
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock FindText(Window window, string text, double size) =>
        Assert.Single(window.GetLogicalDescendants().OfType<TextBlock>(), candidate =>
            candidate.Text == text && Math.Abs(candidate.FontSize - size) < 0.01);

    private static void AssertFamily(App application, string key, string expectedName)
    {
        Assert.True(application.TryFindResource(key, out var resource));
        Assert.Contains(expectedName, Assert.IsType<FontFamily>(resource).Name, StringComparison.Ordinal);
    }

    private static void AssertSize(App application, string key, double expected)
    {
        Assert.True(application.TryFindResource(key, out var resource));
        Assert.Equal(expected, Assert.IsType<double>(resource));
    }

    private static void AssertTextRole(TextBlock? textBlock, string family, double size)
    {
        var actual = Assert.IsType<TextBlock>(textBlock);
        Assert.Contains(family, actual.FontFamily.Name, StringComparison.Ordinal);
        Assert.Equal(size, actual.FontSize);
    }

    private static void AssertControlRole(Control? control, string family, double size)
    {
        var actual = Assert.IsAssignableFrom<TemplatedControl>(control);
        Assert.Contains(family, actual.FontFamily.Name, StringComparison.Ordinal);
        Assert.Equal(size, actual.FontSize);
    }

    private static void EnsureAvaloniaApplication()
    {
        if (Application.Current is null)
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
    }
}
