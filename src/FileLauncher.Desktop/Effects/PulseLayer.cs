using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace FileLauncher.App;

/// <summary>
/// 発光の明滅（演出 glowPulse。spec/EFFECTS.md「常時の演出の詳細」）。枠の発光（FlBoardGlow）を 1 枚の画像に焼いておき、
/// その画像を Opacity 付きで重ねる。BoxShadow を持つ要素の Opacity を直接揺らすと、描き直しのたびにぼかしを計算し直して
/// CPU を 10〜30% 使った（2026-10-04 計測）。画像なら描き直しは貼るだけ。
/// 大きさ・テーマ・基調色が変わったら Refresh で焼き直す。
/// </summary>
internal sealed class PulseLayer : Control
{
    private RenderTargetBitmap? _bitmap;
    private Size _bakedSize;
    private double _bakedScale;
    private object? _bakedShadow;
    private double _margin;

    public PulseLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    private double _level;

    /// <summary>
    /// 明るさ（0〜1）。要素の Opacity ではなく描くときに掛ける（要素の Opacity を 1 未満にすると、上を光の玉が通るたびに
    /// 層全体を合成し直して CPU を 25% 前後使った。2026-10-04 計測）。
    /// </summary>
    public double Level
    {
        get => _level;
        set
        {
            if (value == _level) return;
            _level = value;
            InvalidateVisual();
        }
    }

    /// <summary>今の大きさ・テーマで画像が古ければ焼き直す（盤面が見えていて大きさが決まってから呼ぶ）。</summary>
    public void Refresh()
    {
        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return;
        var variant = (TopLevel.GetTopLevel(this) as IThemeVariantHost)?.ActualThemeVariant ?? ThemeVariant.Default;
        this.TryFindResource("FlBoardGlow", variant, out var shadow);
        this.TryFindResource("FlBoardCornerRadius", variant, out var radius);
        this.TryFindResource("FlGlowMargin", variant, out var marginValue);
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_bitmap is not null && size == _bakedSize && scale == _bakedScale && Equals(shadow, _bakedShadow)) return;

        _margin = marginValue is double m ? m : 0;
        var full = new Size(size.Width + _margin * 2, size.Height + _margin * 2);
        var host = new Border
        {
            Width = size.Width,
            Height = size.Height,
            Margin = new Thickness(_margin),
            Background = Brushes.Transparent,
            CornerRadius = radius is CornerRadius cr ? cr : default,
            BoxShadow = shadow is BoxShadows bs ? bs : default,
        };
        host.Measure(full);
        host.Arrange(new Rect(full));
        var px = new PixelSize(Math.Max(1, (int)Math.Ceiling(full.Width * scale)), Math.Max(1, (int)Math.Ceiling(full.Height * scale)));
        var bitmap = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        bitmap.Render(host);
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _bakedSize = size;
        _bakedScale = scale;
        _bakedShadow = shadow;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap is null || _level <= 0) return;
        using var _ = context.PushOpacity(_level);
        var dest = new Rect(-_margin, -_margin, _bakedSize.Width + _margin * 2, _bakedSize.Height + _margin * 2);
        context.DrawImage(_bitmap, new Rect(_bitmap.Size), dest);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
