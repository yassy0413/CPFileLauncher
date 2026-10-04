using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace FileLauncher.App;

/// <summary>
/// 盤面と窓（設定画面・ダイアログ）で共有する枠（SPEC §3.8）。
/// 自分の Margin = 発光・影が窓の縁で切れないための透明な余白。子は Frame（面・枠線・発光）→ 角ブラケット ×4 → Overlays（最上段の層）。
/// 盤面: Margin FlGlowMarginThickness / 面 FlBoardBackground / 影 FlBoardGlow。窓: FlChromeMarginThickness / FlWindowBackground / FlChromeShadow。
/// </summary>
internal sealed class FrameChrome : Grid
{
    private string _marginKey = "FlGlowMarginThickness";
    private string _backgroundKey = "FlBoardBackground";
    private string _shadowKey = "FlBoardGlow";

    public Border Frame { get; } = new() { Name = "Frame", ClipToBounds = false };

    /// <summary>Frame の上に重ねる層（盤面のグリッチの静止画・ドラッグのゴースト）。</summary>
    public Grid Overlays { get; } = new() { IsHitTestVisible = false, ClipToBounds = false };

    /// <summary>
    /// 面取り（四隅を斜めに切る量 px）。HUD 段階 D（SPEC §3.9 H8）。2026-10-04 に実物を見てユーザーが正式採用。0 なら従来の角丸矩形。
    /// </summary>
    public static double DefaultChamfer { get; set; } = 10;

    private readonly Grid _content = new();       // Frame の中身: 面取りの面（八角形）+ Body
    private readonly Path _face = new() { IsHitTestVisible = false };

    /// <summary>面取りのときに面と枠線を描く八角形（テスト用）。</summary>
    internal Path Face => _face;

    /// <summary>今の面の色（面取りなら八角形の Fill、そうでなければ Border の Background）。</summary>
    internal IBrush? SurfaceBrush => _chamfer > 0 ? _face.Fill : Frame.Background;

    /// <summary>今の枠線の色。</summary>
    internal IBrush? OutlineBrush => _chamfer > 0 ? _face.Stroke : Frame.BorderBrush;
    private readonly Path[] _brackets = new Path[4];
    private bool _surfaceVisible = true;
    private double _chamfer;

    public FrameChrome()
    {
        Frame[!Border.CornerRadiusProperty] = Themed.Res("FlBoardCornerRadius");
        Frame[!Border.BorderThicknessProperty] = Themed.Res("FlBoardBorderThickness");
        _face[!Shape.StrokeProperty] = Themed.Res("FlBoardBorder");
        _content.Children.Add(_face);
        _content.SizeChanged += (_, _) => UpdateChamferGeometry();
        Frame.Child = _content;
        Children.Add(Frame);
        // 4 隅の角ブラケット（枠線から 2 px 外側。左上・右下 = 主色、右上・左下 = 副色。サイバーパンクのみ）
        _brackets[0] = Bracket("FlAccent", new Thickness(-3, -3, 0, 0), HorizontalAlignment.Left, VerticalAlignment.Top);
        _brackets[1] = Bracket("FlAccent2", new Thickness(0, -3, -3, 0), HorizontalAlignment.Right, VerticalAlignment.Top);
        _brackets[2] = Bracket("FlAccent2", new Thickness(-3, 0, 0, -3), HorizontalAlignment.Left, VerticalAlignment.Bottom);
        _brackets[3] = Bracket("FlAccent", new Thickness(0, 0, -3, -3), HorizontalAlignment.Right, VerticalAlignment.Bottom);
        foreach (var b in _brackets) Children.Add(b);
        Children.Add(Overlays);
        Chamfer = DefaultChamfer;
        Bind();
    }

    /// <summary>面取りの量（px）。0 なら角丸矩形（Border の面と枠線）、それ以外は八角形の Path で面と枠線を描き、中身もその形で切り抜く。</summary>
    public double Chamfer
    {
        get => _chamfer;
        set
        {
            _chamfer = Math.Max(0, value);
            UpdateBracketShapes();
            Bind();
            UpdateChamferGeometry();
        }
    }

    public string MarginKey { get => _marginKey; set { _marginKey = value; Bind(); } }
    public string BackgroundKey { get => _backgroundKey; set { _backgroundKey = value; Bind(); } }
    public string ShadowKey { get => _shadowKey; set { _shadowKey = value; Bind(); } }

    /// <summary>枠の中身。</summary>
    [Content]
    public Control? Body
    {
        get => _content.Children.Count > 1 ? _content.Children[1] : null;
        set
        {
            while (_content.Children.Count > 1) _content.Children.RemoveAt(1);
            if (value is not null) _content.Children.Add(value);
        }
    }

    /// <summary>面（地の色）を描くか。盤面の背景画像があるときは描かない（画像の部分からデスクトップが透けるように。SPEC §3.7）。</summary>
    public void SetSurfaceVisible(bool visible)
    {
        _surfaceVisible = visible;
        Bind();
    }

    /// <summary>今のテーマでの余白（片側）。窓の大きさの計算に使う。</summary>
    public double GetMargin()
    {
        var variant = (TopLevel.GetTopLevel(this) as IThemeVariantHost)?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant ?? ThemeVariant.Default;
        return this.TryFindResource(_marginKey, variant, out var v) && v is Thickness t ? t.Left
            : Application.Current?.TryFindResource(_marginKey, variant, out var a) == true && a is Thickness at ? at.Left : 0;
    }

    private void Bind()
    {
        this[!MarginProperty] = Themed.Res(_marginKey);
        Frame[!Border.BoxShadowProperty] = Themed.Res(_shadowKey);
        if (_chamfer > 0)
        {
            // 面と枠線は八角形の Path で描く（Border は角を斜めに切れない）。発光の BoxShadow は Border（矩形）に残す
            Frame.Background = Brushes.Transparent;
            Frame.BorderBrush = Brushes.Transparent;
            _face.IsVisible = true;
            if (_surfaceVisible) _face[!Shape.FillProperty] = Themed.Res(_backgroundKey);
            else _face.Fill = Brushes.Transparent;
        }
        else
        {
            _face.IsVisible = false;
            _content.Clip = null;
            Frame[!Border.BorderBrushProperty] = Themed.Res("FlBoardBorder");
            if (_surfaceVisible) Frame[!Border.BackgroundProperty] = Themed.Res(_backgroundKey);
            else Frame.Background = Brushes.Transparent;
        }
    }

    /// <summary>八角形（面取り c）。</summary>
    private static Geometry Octagon(double x, double y, double w, double h, double c)
    {
        c = Math.Min(c, Math.Min(w, h) / 2);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(x + c, y), true);
            ctx.LineTo(new Point(x + w - c, y));
            ctx.LineTo(new Point(x + w, y + c));
            ctx.LineTo(new Point(x + w, y + h - c));
            ctx.LineTo(new Point(x + w - c, y + h));
            ctx.LineTo(new Point(x + c, y + h));
            ctx.LineTo(new Point(x, y + h - c));
            ctx.LineTo(new Point(x, y + c));
            ctx.EndFigure(true);
        }
        return g;
    }

    /// <summary>面（線の太さの半分だけ内側）と中身の切り抜きを今の大きさで作り直す。</summary>
    private void UpdateChamferGeometry()
    {
        if (_chamfer <= 0) return;
        var size = _content.Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return;
        double t = this.TryFindResource("FlBoardBorderThickness", out var v) && v is Thickness th ? th.Left : 1;
        _face.StrokeThickness = t;
        _face.Data = Octagon(t / 2, t / 2, size.Width - t, size.Height - t, _chamfer);
        _content.Clip = Octagon(0, 0, size.Width, size.Height, _chamfer);
    }

    /// <summary>角ブラケット: 角丸のときは L 字（腕 10 px）、面取りのときは斜辺に沿って折れた線（腕 6 px + 斜辺）。</summary>
    private void UpdateBracketShapes()
    {
        double c = _chamfer, a = 6;
        string[] data = c > 0
            ?
            [
                $"M 0,{c + a} L 0,{c} L {c},0 L {c + a},0",
                $"M 0,0 L {a},0 L {c + a},{c} L {c + a},{c + a}",
                $"M 0,0 L 0,{a} L {c},{c + a} L {c + a},{c + a}",
                $"M {c + a},0 L {c + a},{a} L {a},{c + a} L 0,{c + a}",
            ]
            : ["M 0,10 L 0,0 L 10,0", "M 0,0 L 10,0 L 10,10", "M 0,0 L 0,10 L 10,10", "M 10,0 L 10,10 L 0,10"];
        for (int i = 0; i < 4; i++) _brackets[i].Data = Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", data[i]));
    }

    private static Path Bracket(string strokeKey, Thickness margin, HorizontalAlignment h, VerticalAlignment v)
    {
        var path = new Path { Margin = margin, HorizontalAlignment = h, VerticalAlignment = v, Classes = { "bracket" } };
        path[!Shape.StrokeProperty] = Themed.Res(strokeKey);
        return path;
    }
}
