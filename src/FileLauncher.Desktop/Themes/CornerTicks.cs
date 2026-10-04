using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FileLauncher.App;

/// <summary>
/// 空きスロットの四隅の角マーカー（L 字、腕 5 px、線 1 px。HUD 段階 A。SPEC §3.9）。色はスタイルで Stroke に入れる（ホバーで主色）。
/// </summary>
internal sealed class CornerTicks : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<CornerTicks, IBrush?>(nameof(Stroke));

    static CornerTicks() => AffectsRender<CornerTicks>(StrokeProperty);

    private const double Arm = 5;
    private Pen? _pen;
    private IBrush? _penBrush;

    public CornerTicks()
    {
        IsHitTestVisible = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is null || Bounds.Width < Arm * 2 || Bounds.Height < Arm * 2) return;
        if (!ReferenceEquals(_penBrush, Stroke)) { _penBrush = Stroke; _pen = new Pen(Stroke, 1); } // 色が変わったときだけ作り直す
        double w = Bounds.Width - 0.5, h = Bounds.Height - 0.5, o = 0.5;
        void L(Point corner, double dx, double dy)
        {
            context.DrawLine(_pen!, corner, new Point(corner.X + dx, corner.Y));
            context.DrawLine(_pen!, corner, new Point(corner.X, corner.Y + dy));
        }
        L(new Point(o, o), Arm, Arm);
        L(new Point(w, o), -Arm, Arm);
        L(new Point(o, h), Arm, -Arm);
        L(new Point(w, h), -Arm, -Arm);
    }
}
