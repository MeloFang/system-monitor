using System.Windows;
using System.Windows.Media;

namespace SystemMonitor.Controls;

/// <summary>圆环仪表：底环 + 数值弧（-90° 起），中心文字由 XAML 叠放。</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge), new PropertyMetadata(0.0, (d, _) => ((FrameworkElement)d).InvalidateVisual()));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(double), typeof(RingGauge), new PropertyMetadata(100.0, (d, _) => ((FrameworkElement)d).InvalidateVisual()));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingGauge), new PropertyMetadata(Brushes.White, (d, _) => ((FrameworkElement)d).InvalidateVisual()));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w < 10 || h < 10) return;
        double r = Math.Min(w, h) / 2 - 8;
        var c = new Point(w / 2, h / 2);
        var track = new Pen(new SolidColorBrush(Color.FromRgb(0x06, 0x08, 0x0c)), 11);
        dc.DrawEllipse(null, track, c, r, r);

        var frac = Math.Clamp(Value / Max, 0, 1);
        if (frac <= 0.005) return;
        double a0 = -Math.PI / 2, a1 = a0 + frac * 2 * Math.PI;
        var p0 = new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0));
        var p1 = new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p0, false, false);
            ctx.ArcTo(p1, new Size(r, r), 0, frac > 0.5, SweepDirection.Clockwise, true, false);
        }
        var pen = new Pen(Stroke, 11) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, geo);
    }
}
