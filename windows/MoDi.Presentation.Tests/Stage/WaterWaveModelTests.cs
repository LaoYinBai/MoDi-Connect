using MoDi.Presentation.Stage;

namespace MoDi.Presentation.Tests.Stage;

public sealed class WaterWaveModelTests
{
    [Fact]
    public void Quiet_water_stays_nearly_flat()
    {
        var layers = WaterWaveModel.Create(width: 200, baseline: 80, rms: 0, elapsedSeconds: 0);

        Assert.Equal(3, layers.Count);
        Assert.All(layers, layer =>
        {
            var range = layer.Max(point => point.Y) - layer.Min(point => point.Y);
            Assert.InRange(range, 0, 2.5);
        });
    }

    [Fact]
    public void Peak_rms_increases_amplitude_without_moving_mean_waterline()
    {
        var quiet = WaterWaveModel.Create(400, 80, 0, 0.25);
        var peak = WaterWaveModel.Create(400, 80, 1, 0.25);

        var quietRange = quiet.Max(layer => layer.Max(point => point.Y) - layer.Min(point => point.Y));
        var peakRange = peak.Max(layer => layer.Max(point => point.Y) - layer.Min(point => point.Y));
        var peakMean = peak.SelectMany(layer => layer).Average(point => point.Y);

        Assert.True(peakRange > quietRange * 3);
        Assert.InRange(peakMean, 78, 82);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void Rms_is_clamped_before_wave_generation(double input, double clamped)
    {
        var actual = WaterWaveModel.Create(100, 80, input, 0.5);
        var expected = WaterWaveModel.Create(100, 80, clamped, 0.5);

        Assert.Equal(expected, actual);
    }
}
