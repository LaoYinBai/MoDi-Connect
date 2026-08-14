using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MoDi.Presentation.Tests.Typography;

[Collection("Avalonia UI")]
public sealed class TypographyContractTests
{
    [Fact]
    public void Caption_role_uses_the_annotation_family_at_the_caption_size()
    {
        TestApplicationHost.Ensure();
        var caption = new TextBlock { Text = "状态说明" };
        caption.Classes.Add("caption");
        var window = new Window { Content = caption };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("MoDi UI Annotation GenYo Mincho", caption.FontFamily.Name, StringComparison.Ordinal);
            Assert.Equal(12d, caption.FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Typography_resources_expose_the_five_design_roles_and_exact_sizes()
    {
        var application = TestApplicationHost.Ensure();

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

    private static void AssertFamily(Application application, string key, string expectedName)
    {
        Assert.True(application.TryFindResource(key, out var resource), $"Missing typography resource: {key}");
        var family = Assert.IsType<FontFamily>(resource);
        Assert.Contains(expectedName, family.Name, StringComparison.Ordinal);
    }

    private static void AssertSize(Application application, string key, double expected)
    {
        Assert.True(application.TryFindResource(key, out var resource), $"Missing typography resource: {key}");
        Assert.Equal(expected, Assert.IsType<double>(resource));
    }
}
