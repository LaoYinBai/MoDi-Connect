using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Tests.Stage;

public sealed class InkRevealModelTests
{
    [Theory]
    [InlineData(0.999)]
    [InlineData(1.0)]
    public void Completed_reveal_removes_mask_and_is_fully_opaque(double progress)
    {
        var state = InkRevealModel.FromProgress(progress);

        Assert.False(state.UsesMask);
        Assert.Equal(1, state.Opacity);
        Assert.Equal(progress * 1.35, state.Radius, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Empty_or_invalid_reveal_stays_hidden_behind_initial_mask(double progress)
    {
        var state = InkRevealModel.FromProgress(progress);

        Assert.True(state.UsesMask);
        Assert.Equal(0, state.Opacity);
        Assert.Equal(0.001, state.Radius, 6);
    }
}
