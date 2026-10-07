using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using FileLauncher.Core.Theming;

namespace FileLauncher.App;

/// <summary>
/// 面取りした枠（八角形）に沿うネオンの発光（SPEC §3.9「面取りの仕様」、issue/CHAMFER_GLOW.md）。
/// BoxShadow は矩形しか描けないので、Core NeonProfile の断面（BoxShadow と同じガウス）を、八角形をオフセットした帯（0.5 px 幅）で描く。
/// Effect も BoxShadow も使わないので RenderTargetBitmap にそのまま焼ける（明滅 PulseLayer も同じ <see cref="NeonGlowPlan"/> を焼く）。
/// 自分の Bounds が枠の外形。外側の光は Bounds の外（窓の余白）へ描く。
/// </summary>
internal sealed class NeonGlowLayer : Control
{
    public enum GlowPart { Outer, Inner }

    public static readonly StyledProperty<BoxShadows> ShadowsProperty = AvaloniaProperty.Register<NeonGlowLayer, BoxShadows>(nameof(Shadows));

    static NeonGlowLayer() => AffectsRender<NeonGlowLayer>(ShadowsProperty);

    private NeonGlowPlan? _plan;

    public NeonGlowLayer(GlowPart part)
    {
        Part = part;
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public GlowPart Part { get; }

    public BoxShadows Shadows
    {
        get => GetValue(ShadowsProperty);
        set => SetValue(ShadowsProperty, value);
    }

    private double _chamfer;

    public double Chamfer
    {
        get => _chamfer;
        set { if (value != _chamfer) { _chamfer = value; InvalidateVisual(); } }
    }

    public override void Render(DrawingContext context)
    {
        var frame = new Rect(Bounds.Size);
        if (_plan is null || !_plan.Matches(frame, _chamfer, Shadows))
            _plan = NeonGlowPlan.Create(frame, _chamfer, Shadows, outer: Part == GlowPart.Outer, inner: Part == GlowPart.Inner);
        _plan.Draw(context);
    }
}

/// <summary>
/// 発光の描き方 1 組（ジオメトリとブラシを作り置きし、描くときは塗るだけ）。描画の唯一の実装。
/// 大きさ・面取り・影の値が変わったら作り直す（Render の中で毎回 new しない）。
/// </summary>
internal sealed class NeonGlowPlan
{
    private readonly Rect _frame;
    private readonly double _chamfer;
    private readonly BoxShadows _shadows;
    private readonly List<(Geometry Shape, IBrush Brush)> _bands = [];

    private NeonGlowPlan(Rect frame, double chamfer, BoxShadows shadows)
    {
        _frame = frame;
        _chamfer = chamfer;
        _shadows = shadows;
    }

    public bool Matches(Rect frame, double chamfer, BoxShadows shadows) =>
        frame == _frame && chamfer == _chamfer && shadows == _shadows;

    /// <param name="frame">枠の外形（八角形を内接させる矩形）。</param>
    /// <param name="outer">外側の光（BoxShadow の外向きの層）を描くか。</param>
    /// <param name="inner">内側の光（IsInset の層）を描くか。</param>
    public static NeonGlowPlan Create(Rect frame, double chamfer, BoxShadows shadows, bool outer, bool inner)
    {
        var plan = new NeonGlowPlan(frame, chamfer, shadows);
        if (frame.Width < 1 || frame.Height < 1) return plan;
        var (outerLayers, outerColor) = Layers(shadows, inset: false);
        var (innerLayers, innerColor) = Layers(shadows, inset: true);
        // 帯 = 距離 Outer の八角形 − 距離 Inner の八角形（外側は膨らませ、内側は縮める）
        if (outer && outerLayers.Count > 0)
            foreach (var (o, i, a) in NeonProfile.Bands(outerLayers, AmbientAnimator.MaxPulseGain))
                plan._bands.Add((Band(Offset(frame, o, chamfer), Offset(frame, i, chamfer)), Brush(outerColor, a)));
        if (inner && innerLayers.Count > 0)
            foreach (var (o, i, a) in NeonProfile.Bands(innerLayers, AmbientAnimator.MaxPulseGain))
                plan._bands.Add((Band(Offset(frame, -i, chamfer), Offset(frame, -o, chamfer)), Brush(innerColor, a)));
        return plan;
    }

    public void Draw(DrawingContext context)
    {
        foreach (var (shape, brush) in _bands) context.DrawGeometry(brush, null, shape);
    }

    /// <summary>外形を distance だけ外へ（負なら内へ）オフセットした八角形。縮めすぎて無くなれば null。</summary>
    private static Geometry? Offset(Rect frame, double distance, double chamfer)
    {
        var r = frame.Inflate(distance);
        return r.Width <= 0 || r.Height <= 0 ? null : Octagon(r, Core.Theming.Octagon.OffsetChamfer(chamfer, distance));
    }

    private static Geometry Band(Geometry? outer, Geometry? inner) =>
        inner is null ? outer! : new CombinedGeometry(GeometryCombineMode.Exclude, outer!, inner);

    /// <summary>BoxShadows → 断面の層（外向き / 内向き）と色（Neon は 1 色。最初の層の色を使う）。</summary>
    private static (List<NeonLayer> Layers, Color Color) Layers(BoxShadows shadows, bool inset)
    {
        var layers = new List<NeonLayer>();
        Color color = default;
        foreach (var s in shadows)
        {
            if (s.IsInset != inset || s.Color.A == 0) continue;
            if (layers.Count == 0) color = s.Color;
            layers.Add(new NeonLayer(s.Blur, s.Color.A / 255.0));
        }
        return (layers, color);
    }

    private static IBrush Brush(Color c, double alpha) => new ImmutableSolidColorBrush(Color.FromRgb(c.R, c.G, c.B), alpha);

    /// <summary>八角形のジオメトリ（形は Core Octagon）。</summary>
    public static Geometry Octagon(Rect r, double chamfer)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open()) AddOctagon(ctx, r, chamfer);
        return g;
    }

    private static void AddOctagon(StreamGeometryContext ctx, Rect r, double chamfer)
    {
        var p = Core.Theming.Octagon.Points(r.X, r.Y, r.Width, r.Height, chamfer);
        ctx.BeginFigure(new Point(p[0].X, p[0].Y), true);
        for (int i = 1; i < p.Length; i++) ctx.LineTo(new Point(p[i].X, p[i].Y));
        ctx.EndFigure(true);
    }
}
