using MoDi.Presentation.Shell;

namespace MoDi.Presentation.Tests.Shell;

public sealed class TopBarLayoutTests
{
    [Theory]
    [InlineData(1280, 220, 164, 640)]
    [InlineData(1280, 80, 300, 640)]
    public void Navigation_center_is_the_window_center_regardless_of_side_widths(
        double windowWidth,
        double leftWidth,
        double rightWidth,
        double expectedCenter)
    {
        var layout = TopBarLayout.Calculate(windowWidth, leftWidth, rightWidth);

        Assert.Equal(expectedCenter, layout.NavigationCenterX);
    }
}
