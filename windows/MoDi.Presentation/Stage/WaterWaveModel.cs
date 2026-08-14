namespace MoDi.Presentation.Stage;

public readonly record struct WaterWavePoint(double X, double Y);

public static class WaterWaveModel
{
    private const double SampleSpacing = 2;
    private const double TravelSpeed = 40;

    public static IReadOnlyList<IReadOnlyList<WaterWavePoint>> Create(
        double width,
        double baseline,
        double rms,
        double elapsedSeconds)
    {
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        var normalizedRms = double.IsFinite(rms) ? Math.Clamp(rms, 0, 1) : 0;
        var layers = new IReadOnlyList<WaterWavePoint>[3];
        var baseAmplitudes = new[] { 0.8, 0.55, 0.35 };
        var rmsAmplitudes = new[] { 5.2, 3.8, 2.6 };
        var wavelengths = new[] { 96d, 128d, 168d };
        var phases = new[] { 0d, Math.PI * 0.62, Math.PI * 1.18 };
        var waterlineOffsets = new[] { -8d, 0d, 8d };

        for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            var points = new List<WaterWavePoint>((int)Math.Ceiling(width / SampleSpacing) + 1);
            var amplitude = baseAmplitudes[layerIndex] + rmsAmplitudes[layerIndex] * normalizedRms;
            var frequency = 2 * Math.PI / wavelengths[layerIndex];

            for (var x = 0d; x <= width; x += SampleSpacing)
            {
                var traveledX = x - TravelSpeed * elapsedSeconds;
                var y = baseline + waterlineOffsets[layerIndex] +
                    amplitude * Math.Sin(traveledX * frequency + phases[layerIndex]);
                points.Add(new WaterWavePoint(x, y));
            }

            layers[layerIndex] = points;
        }

        return layers;
    }
}
