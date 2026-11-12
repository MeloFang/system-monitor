using System.Windows;
using System.Windows.Media;

namespace SystemMonitor.Controls;

/// <summary>近 60s 趋势曲线：折线 + 渐隐面积填充，色相取自系列色。</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new PropertyMetadata(null, (d, _) => ((FrameworkElement)d).InvalidateVisual()));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new PropertyMetadata(Brushes.White, OnStrokeChanged));

    private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var el = (FrameworkElement)d;
        var col = (e.NewValue as SolidColorBrush)?.Color ?? Colors.White;
        el.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = col, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.7,
        };
        el.InvalidateVisual();
    }
    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
        nameof(Min), typeof(double), typeof(Sparkline), new PropertyMetadata(0.0, (d, _) => ((FrameworkElement)d).InvalidateVisual()));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(double), typeof(Sparkline), new PropertyMetadata(100.0, (d, _) => ((FrameworkElement)d).InvalidateVisual()));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w < 4 || h < 4) return;
        var data = Values;
        if (data is null || data.Count < 2) return;

        double X(int i) => data.Count < 2 ? 0 : i / (double)(data.Count - 1) * w;
        double Y(double v) => h - 2 - (Math.Clamp(v, Min, Max) - Min) / (Max - Min) * (h - 4);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(X(0), Y(data[0])), false, false);
            for (int i = 1; i < data.Count; i++) ctx.LineTo(new Point(X(i), Y(data[i])), true, false);
        }
        var pen = new Pen(Stroke, 1.6) { LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, pen, geo);

        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(new Point(X(0), h), true, true);
            for (int i = 0; i < data.Count; i++) ctx.LineTo(new Point(X(i), Y(data[i])), false, false);
            ctx.LineTo(new Point(X(data.Count - 1), h), false, false);
        }
        var color = (Stroke as SolidColorBrush)?.Color ?? Colors.White;
        var grad = new LinearGradientBrush(
            Color.FromArgb(0x33, color.R, color.G, color.B),
            Color.FromArgb(0x00, color.R, color.G, color.B), 90);
        dc.DrawGeometry(grad, null, fill);
    }
}
