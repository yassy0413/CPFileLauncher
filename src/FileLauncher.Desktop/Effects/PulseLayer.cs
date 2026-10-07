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
/// 面取りした枠では静的な発光と同じ NeonGlowPlan（八角形に沿う）を焼く。大きさ・配色・面取りが変わったら Refresh で焼き直す。
/// </summary>
internal sealed class PulseLayer : Control
{
    private RenderTargetBitmap? _bitmap;
    private Size _bakedSize;
    private double _bakedScale;
    private object? _bakedShadow;
    private double _bakedChamfer = -1;
    private double _margin;

    /// <summary>枠の面取り（BoardWindow が FrameChrome.Chamfer を入れる）。0 なら従来の角丸矩形の BoxShadow を焼く。</summary>
    public double Chamfer { get; set; }

    /// <summary>焼いた画像（テスト用）。</summary>
    internal Bitmap? BakedImage => _bitmap;

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
        double margin = marginValue is double m ? m : 0;
        if (_bitmap is not null && size == _bakedSize && scale == _bakedScale && Equals(shadow, _bakedShadow)
            && Chamfer == _bakedChamfer && margin == _margin) return;

        _margin = margin;
        var full = new Size(size.Width + _margin * 2, size.Height + _margin * 2);
        var px = new PixelSize(Math.Max(1, (int)Math.Ceiling(full.Width * scale)), Math.Max(1, (int)Math.Ceiling(full.Height * scale)));
        var bitmap = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        if (Chamfer > 0)
        {
            // 面取り: 静的な発光（FrameChrome の NeonGlowLayer）と同じ描き方を焼く。発光は枠線の外形（Frame の枠の太さの内側）から始まる
            this.TryFindResource("FlBoardBorderThickness", variant, out var thicknessValue);
            double t = thicknessValue is Thickness th ? th.Left : 1;
            var plan = NeonGlowPlan.Create(new Rect(t, t, size.Width - 2 * t, size.Height - 2 * t), Chamfer,
                shadow is BoxShadows glow ? glow : default, outer: true, inner: true);
            using (var ctx = bitmap.CreateDrawingContext())
            using (ctx.PushTransform(Matrix.CreateTranslation(_margin, _margin)))
                plan.Draw(ctx);
        }
        else
        {
            RenderRoundedRect(bitmap, size, full, shadow, radius);
        }
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _bakedSize = size;
        _bakedScale = scale;
        _bakedShadow = shadow;
        _bakedChamfer = Chamfer;
        InvalidateVisual();
    }

    /// <summary>面取りなし（角丸矩形。テスト用の Chamfer = 0）: BoxShadow を持つ Border をそのまま焼く。</summary>
    private void RenderRoundedRect(RenderTargetBitmap bitmap, Size size, Size full, object? shadow, object? radius)
    {
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
        bitmap.Render(host);
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
