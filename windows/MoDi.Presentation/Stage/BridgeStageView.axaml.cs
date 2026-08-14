using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace MoDi.Presentation.Stage;

public partial class BridgeStageView : UserControl
{
    private readonly DispatcherTimer _renderTimer;
    private readonly Stopwatch _stageClock = new();
    private readonly List<Bitmap> _bitmaps = [];
    private readonly PathGeometry[] _waterGeometries =
        [new PathGeometry(), new PathGeometry(), new PathGeometry()];
    private CroppedBitmap[] _walkFrames = [];
    private CroppedBitmap[] _idleFrames = [];
    private Bitmap? _inkTraceDark;
    private Bitmap? _inkTraceLight;
    private RadialGradientBrush? _bridgeRevealMask;
    private RadialGradientBrush? _boyRevealMask;
    private bool _assetsLoaded;

    public BridgeStageView()
    {
        InitializeComponent();
        var bridgeTransformOrigin = new RelativePoint(
            0.5,
            InkStageLayout.BridgeBaselineY / InkStageLayout.StageHeight,
            RelativeUnit.Relative);
        BridgeGrayImage.RenderTransformOrigin = bridgeTransformOrigin;
        BridgeColorImage.RenderTransformOrigin = bridgeTransformOrigin;
        BridgeGrayImage.RenderTransform = new ScaleTransform(1, InkStageLayout.BridgeScaleY);
        BridgeColorImage.RenderTransform = new ScaleTransform(1, InkStageLayout.BridgeScaleY);

        WaterPathBack.Data = _waterGeometries[0];
        WaterPathMiddle.Data = _waterGeometries[1];
        WaterPathFront.Data = _waterGeometries[2];

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += OnRenderTick;
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        ActualThemeVariantChanged += OnThemeChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs) => RenderStage();

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        if (!TryLoadAssets())
            return;

        _stageClock.Restart();
        _renderTimer.Start();
        ApplyThemeTexture();
        RenderStage();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        _renderTimer.Stop();
        _stageClock.Stop();
        DisposeAssets();
    }

    private void OnRenderTick(object? sender, EventArgs eventArgs) => RenderStage();

    private void OnThemeChanged(object? sender, EventArgs eventArgs) => ApplyThemeTexture();

    private bool TryLoadAssets()
    {
        if (_assetsLoaded)
            return true;

        try
        {
            LeftBankImage.Source = Load("bank-left.png");
            RightBankImage.Source = Load("bank-right.png");
            BridgeGrayImage.Source = Load("bridge-gray.png");
            BridgeColorImage.Source = Load("bridge-color.png");
            var idleSheet = Load("boy-idle-sheet.png");
            var walkSheet = Load("boy-walk-sheet.png");
            StandingBoyGrayImage.Source = Load("boy-stand-gray.png");
            StandingBoyColorImage.Source = Load("boy-stand-color.png");
            _inkTraceDark = Load("ink-trace-dark.png");
            _inkTraceLight = Load("ink-trace-light.png");

            _walkFrames = new CroppedBitmap[16];
            for (var index = 0; index < _walkFrames.Length; index++)
            {
                _walkFrames[index] = new CroppedBitmap(
                    walkSheet,
                    new PixelRect((index % 4) * 250, (index / 4) * 100, 250, 100));
            }

            _idleFrames = new CroppedBitmap[4];
            for (var index = 0; index < _idleFrames.Length; index++)
            {
                _idleFrames[index] = new CroppedBitmap(
                    idleSheet,
                    new PixelRect((index % 2) * 500, (index / 2) * 200, 500, 200));
            }

            _bridgeRevealMask = CreateRevealMask(0.43, 0.56);
            _boyRevealMask = CreateRevealMask(0.80, 0.64);
            BridgeColorImage.OpacityMask = _bridgeRevealMask;
            StandingBoyColorImage.OpacityMask = _boyRevealMask;
            AssetErrorPanel.IsVisible = false;
            _assetsLoaded = true;
            return true;
        }
        catch (Exception exception)
        {
            DisposeAssets();
            AssetErrorText.Text = $"水墨舞台资产加载失败\n{exception.Message}";
            AssetErrorPanel.IsVisible = true;
            return false;
        }
    }

    private Bitmap Load(string fileName)
    {
        var uri = new Uri($"avares://MoDi.Presentation/Assets/Stage/{fileName}");
        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        _bitmaps.Add(bitmap);
        return bitmap;
    }

    private void RenderStage()
    {
        if (!_assetsLoaded || DataContext is not BridgeStageViewModel viewModel)
            return;

        var presentation = InkStagePresentation.FromFrame(
            viewModel.Frame,
            _stageClock.Elapsed.TotalSeconds,
            viewModel.Rms);

        RenderBoy(presentation);
        RenderReveal(_bridgeRevealMask, BridgeColorImage, presentation.ColorReveal);
        RenderReveal(_boyRevealMask, StandingBoyColorImage, presentation.ColorReveal);
        RenderWater(presentation, _stageClock.Elapsed.TotalSeconds);
    }

    private void RenderBoy(InkStagePresentation presentation)
    {
        WalkBoyImage.IsVisible = presentation.BoyMode == InkBoyMode.Walk;
        IdleBoyImage.IsVisible = presentation.BoyMode == InkBoyMode.Idle;
        StandingBoyGrayImage.IsVisible = presentation.BoyMode == InkBoyMode.Stand;
        StandingBoyColorImage.IsVisible = presentation.BoyMode == InkBoyMode.Stand;

        if (WalkBoyImage.IsVisible)
        {
            WalkBoyImage.Source = _walkFrames[Math.Clamp(presentation.BoyFrame, 0, 15)];
            Canvas.SetLeft(WalkBoyImage, presentation.BoyCanvasLeft);
            Canvas.SetTop(WalkBoyImage, presentation.BoyCanvasTop);
            WalkBoyImage.RenderTransformOrigin = RelativePoint.Center;
            WalkBoyImage.RenderTransform = presentation.BoyMirrored
                ? new ScaleTransform(-1, 1)
                : new ScaleTransform(1, 1);
        }

        if (IdleBoyImage.IsVisible)
        {
            IdleBoyImage.Source = _idleFrames[Math.Clamp(presentation.BoyFrame, 0, 3)];
            Canvas.SetLeft(IdleBoyImage, presentation.BoyCanvasLeft);
            Canvas.SetTop(IdleBoyImage, presentation.BoyCanvasTop);
        }
    }

    private void RenderWater(InkStagePresentation presentation, double seconds)
    {
        var layers = WaterWaveModel.Create(1000, 326, presentation.EffectiveRms, seconds);
        for (var index = 0; index < _waterGeometries.Length; index++)
            ReplaceGeometry(_waterGeometries[index], layers[index]);

        WaterCanvas.Opacity = presentation.WaterLevel;
        WaterCanvas.RenderTransform = new TranslateTransform(0, (1 - presentation.WaterLevel) * 74);
    }

    private static void ReplaceGeometry(PathGeometry geometry, IReadOnlyList<WaterWavePoint> points)
    {
        if (points.Count == 0)
        {
            geometry.Figures = [];
            return;
        }

        var figure = new PathFigure { StartPoint = new Point(points[0].X, points[0].Y), IsClosed = false };
        for (var index = 1; index < points.Count; index++)
            figure.Segments!.Add(new LineSegment { Point = new Point(points[index].X, points[index].Y) });
        geometry.Figures = [figure];
    }

    private static RadialGradientBrush CreateRevealMask(double x, double y)
    {
        var origin = new RelativePoint(x, y, RelativeUnit.Relative);
        var brush = new RadialGradientBrush
        {
            Center = origin,
            GradientOrigin = origin,
            RadiusX = new RelativeScalar(0.001, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.001, RelativeUnit.Relative),
        };
        brush.GradientStops.Add(new GradientStop(Colors.White, 0));
        brush.GradientStops.Add(new GradientStop(Colors.White, 0.82));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        return brush;
    }

    private static void RenderReveal(RadialGradientBrush? mask, Control image, double reveal)
    {
        if (mask is null)
            return;

        var state = InkRevealModel.FromProgress(reveal);
        image.Opacity = state.Opacity;
        if (!state.UsesMask)
        {
            image.OpacityMask = null;
            return;
        }

        image.OpacityMask = mask;
        mask.RadiusX = new RelativeScalar(state.Radius, RelativeUnit.Relative);
        mask.RadiusY = new RelativeScalar(state.Radius, RelativeUnit.Relative);
    }

    private void ApplyThemeTexture()
    {
        if (!_assetsLoaded)
            return;
        InkTraceImage.Source = ActualThemeVariant == ThemeVariant.Dark ? _inkTraceDark : _inkTraceLight;
    }

    private void DisposeAssets()
    {
        BridgeColorImage.OpacityMask = null;
        StandingBoyColorImage.OpacityMask = null;
        _bridgeRevealMask = null;
        _boyRevealMask = null;
        _walkFrames = [];
        _idleFrames = [];

        foreach (var bitmap in _bitmaps)
            bitmap.Dispose();
        _bitmaps.Clear();
        _inkTraceDark = null;
        _inkTraceLight = null;
        _assetsLoaded = false;
    }
}
