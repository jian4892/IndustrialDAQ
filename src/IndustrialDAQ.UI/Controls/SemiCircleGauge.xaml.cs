// File: SemiCircleGauge.xaml.cs  Module: UI (Controls)  Author: IndustrialDAQ Team
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IndustrialDAQ.UI.Controls;

/// <summary>
/// 半圆仪表盘控件 — 180° 弧形仪表，用于显示温度、压力等模拟量。
/// 支持动画、颜色自定义和数值范围配置。
/// </summary>
public partial class SemiCircleGauge : UserControl
{
    // ─── 依赖属性 ───

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata("unit", FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata("Gauge", FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    public static readonly DependencyProperty GaugeColorProperty =
        DependencyProperty.Register(nameof(GaugeColor), typeof(Brush), typeof(SemiCircleGauge),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                FrameworkPropertyMetadataOptions.AffectsRender, OnAppearanceChanged));

    // ─── CLR 包装属性 ───

    /// <summary>当前测量值。</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>量程下限。</summary>
    public double MinValue { get => (double)GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }

    /// <summary>量程上限。</summary>
    public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    /// <summary>量程上限（别名，与 MaxValue 共享同一依赖属性）。</summary>
    public double Maximum { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    /// <summary>单位字符串。</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>仪表标签。</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>弧形颜色画刷。</summary>
    public Brush GaugeColor { get => (Brush)GetValue(GaugeColorProperty); set => SetValue(GaugeColorProperty, value); }

    /// <summary>弧形颜色画刷（别名，与 GaugeColor 共享同一依赖属性）。</summary>
    public Brush ArcColor { get => (Brush)GetValue(GaugeColorProperty); set => SetValue(GaugeColorProperty, value); }

    // ─── 构造 ───

    public SemiCircleGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => DrawGauge();
        SizeChanged += (_, _) => DrawGauge();
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SemiCircleGauge gauge)
            gauge.DrawGauge();
    }

    /// <summary>
    /// 绘制 180° 弧形仪表。
    /// 起点 180°（左侧）→ 终点 0°（右侧），顺时针绘制底部半圆。
    /// </summary>
    private void DrawGauge()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 1 || h < 1) return;

        double thickness = 12;
        double cx = w / 2.0;
        double cy = h * 0.82;                  // 圆心偏下
        double r = Math.Min(w, h * 1.65) / 2.0 - thickness;

        if (r < 20) return;

        // ── 背景弧（全 180°） ──
        BackgroundArc.Data = CreateArc(cx, cy, r, thickness, 180.0, 180.0);

        // ── 值弧（比例角度） ──
        double fraction = Math.Clamp((Value - MinValue) / (MaxValue - MinValue), 0, 1);
        double sweepAngle = 180.0 * fraction;

        ValueArc.Data = CreateArc(cx, cy, r, thickness, 180.0, sweepAngle);
        ValueArc.Stroke = GaugeColor;

        // ── 中心文字 ──
        ValueText.Text = Value.ToString("F1");
        UnitText.Text = Unit;
        LabelText.Text = Label;

        // 将文本上移到弧中心附近
        ValueText.Margin = new Thickness(0, 0, 0, r * 0.35);
    }

    /// <summary>
    /// 创建弧形 PathGeometry。
    /// 使用三角函数将角度转换为圆弧端点坐标。
    /// </summary>
    /// <param name="startAngle">弧起点角度（WPF 坐标系，Y 轴向下），0°=右, 180°=左</param>
    /// <param name="sweepAngle">顺时针扫描角度</param>
    private static PathGeometry CreateArc(double cx, double cy, double r,
        double thickness, double startAngle, double sweepAngle)
    {
        if (sweepAngle < 0.5) sweepAngle = 0.5; // 最小可见弧

        double startRad = startAngle * Math.PI / 180.0;
        double endRad = (startAngle - sweepAngle) * Math.PI / 180.0;

        // 三角函数计算端点坐标
        double x1 = cx + r * Math.Cos(startRad);
        double y1 = cy - r * Math.Sin(startRad);
        double x2 = cx + r * Math.Cos(endRad);
        double y2 = cy - r * Math.Sin(endRad);

        var figure = new PathFigure { StartPoint = new Point(x1, y1) };
        var arc = new ArcSegment
        {
            Point = new Point(x2, y2),
            Size = new Size(r, r),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweepAngle > 180
        };
        figure.Segments.Add(arc);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
