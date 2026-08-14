namespace MoDi.Presentation.Shell;

public readonly record struct TopBarLayout(double NavigationCenterX)
{
    public static TopBarLayout Calculate(
        double windowWidth,
        double leftContentWidth,
        double rightContentWidth)
    {
        _ = leftContentWidth;
        _ = rightContentWidth;
        return new TopBarLayout(Math.Max(0, windowWidth) / 2d);
    }
}
