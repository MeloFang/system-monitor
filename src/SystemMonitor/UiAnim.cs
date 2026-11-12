using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SystemMonitor;

/// <summary>原型动效复刻：视图/卡片入场（viewIn/cardIn）与状态点脉冲（pulse）。</summary>
public static class UiAnim
{
    public static void Enter(FrameworkElement el, double delaySec = 0)
    {
        el.Opacity = 0;
        var tt = new TranslateTransform(0, 14);
        el.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var op = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.45)) { BeginTime = TimeSpan.FromSeconds(delaySec), EasingFunction = ease };
        var ty = new DoubleAnimation(14, 0, TimeSpan.FromSeconds(0.45)) { BeginTime = TimeSpan.FromSeconds(delaySec), EasingFunction = ease };
        el.BeginAnimation(UIElement.OpacityProperty, op);
        tt.BeginAnimation(TranslateTransform.YProperty, ty);
    }

    public static void Pulse(FrameworkElement dot, System.Windows.Media.Effects.Effect glow)
    {
        dot.Effect = glow;
        var a = new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(0.8))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        dot.BeginAnimation(UIElement.OpacityProperty, a);
    }

    public static void StopPulse(FrameworkElement dot)
    {
        dot.BeginAnimation(UIElement.OpacityProperty, null);
        dot.Opacity = 1;
        dot.Effect = null;
    }
}
