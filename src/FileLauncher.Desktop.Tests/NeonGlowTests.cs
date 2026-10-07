using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FileLauncher.App;
using FileLauncher.Core.Theming;

namespace FileLauncher.Desktop.Tests;

/// <summary>
/// 八角形（面取り）に沿う枠の発光（SPEC §3.9「面取りの仕様」、issue/CHAMFER_GLOW.md）。実際の描画（Skia）で画素を読む。
/// 黒地に描くので、主色シアン（G = 229）の G の値が「発光の不透明度 × 229」になる。
/// </summary>
public sealed class NeonGlowTests
{
    private const int W = 240, H = 160;
    private const double Chamfer = 10;

    /// <summary>面を描かない枠（発光だけ）を黒地の窓に置いて描く。戻り値は窓の画素と、発光が始まる外形の左上。</summary>
    private static (Func<int, int, int> G, double Edge, double Margin, Window Window) Draw(Action<FrameChrome>? setup = null)
    {
        var chrome = new FrameChrome { Width = W, Height = H, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        chrome.SetSurfaceVisible(false);
        foreach (var bracket in chrome.Children.OfType<Avalonia.Controls.Shapes.Path>()) bracket.IsVisible = false; // 角ブラケットは発光と別物
        setup?.Invoke(chrome);
        var window = new Window { Background = Brushes.Black, Width = 400, Height = 300, Content = chrome };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        double margin = chrome.GetMargin();
        double t = window.TryFindResource("FlBoardBorderThickness", out var v) && v is Thickness th ? th.Left : 1;
        var frame = window.CaptureRenderedFrame()!;
        var g = Read(frame);
        return (g, margin + t, margin, window);
    }

    /// <summary>ビットマップ（BGRA）の G を読む関数。</summary>
    private static Func<int, int, int> Read(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        int stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride); }
        finally { handle.Free(); }
        return (x, y) => x < 0 || y < 0 || x >= size.Width || y >= size.Height ? 0 : buffer[y * stride + x * 4 + 1];
    }

    /// <summary>
    /// 画素の比較の許容: ±4 と ±20% の大きい方。静的な層は配置の丸め（枠の太さ 1.5 → 整数）で最大 0.5 px ずれ、
    /// 辺の近くの急な所ではその分だけ値が変わる。
    /// </summary>
    private static void AssertNear(int expected, int actual) =>
        Assert.InRange(actual, expected - Math.Max(4, expected * 0.2), expected + Math.Max(4, expected * 0.2));

    [AvaloniaFact]
    public void 発光は辺から外へ単調に弱まり_窓の余白の内側で消えきる()
    {
        var (g, edge, margin, window) = Draw();
        int x = (int)(edge + W / 2.0), top = (int)Math.Floor(edge);
        Assert.True(g(x, top - 1) > 60, $"辺のすぐ外が光る: {g(x, top - 1)}");
        for (int d = 1; d < top; d++) Assert.True(g(x, top - d - 1) <= g(x, top - d) + 1, $"d={d}: {g(x, top - d - 1)} > {g(x, top - d)}");
        Assert.True(g(x, 1) <= 1, $"窓の端の 2 px 手前で消えている: {g(x, 1)}");
        Assert.True(NeonProfile.Extent([new(4, 0xE6 / 255.0), new(14, 0x8C / 255.0), new(32, 0x4D / 255.0)], AmbientAnimator.MaxPulseGain) + 2 <= margin);
        window.Close();
    }

    [AvaloniaFact]
    public void 角で四角く光らず_斜辺の外も辺と同じ断面で光る()
    {
        var (g, edge, _, window) = Draw();
        int top = (int)Math.Floor(edge), mid = (int)(edge + W / 2.0);
        int Top(int d) => g(mid, top - 1 - d);
        // 切り落とした角（外形の矩形の左上）は斜辺から c/√2 ≈ 7 px 外 → 上辺から 7 px 外と同じくらいで、辺のすぐ外より十分暗い
        int corner = g(top, top);
        Assert.InRange(corner, Top(6) - 6, Top(6) + 6);
        Assert.True(corner < Top(0) / 2, $"角 {corner} / 辺 {Top(0)}");
        // 左上の斜辺の中点から外向きの法線（−1, −1）/√2 に沿った画素（中心がちょうど法線の上に乗る k 画素ずつ斜めの点。距離 k√2）
        int c0 = (int)Math.Floor(edge + Chamfer / 2);
        double TopAt(double d) // 上辺から距離 d の値（画素の中心の間を線形補間）
        {
            double y = edge - d - 0.5;
            int y0 = (int)Math.Floor(y);
            return g(mid, y0) + (g(mid, y0 + 1) - g(mid, y0)) * (y - y0);
        }
        foreach (int k in new[] { 2, 4, 8 })
            AssertNear((int)Math.Round(TopAt(k * Math.Sqrt(2) + (edge + Chamfer / 2 - c0 - 0.5) * Math.Sqrt(2))), g(c0 - k, c0 - k));
        window.Close();
    }

    [AvaloniaFact]
    public void 内側の光は枠線のすぐ内側に染みて内へ弱まる()
    {
        var (g, edge, _, window) = Draw();
        int x = (int)(edge + W / 2.0), top = (int)Math.Ceiling(edge);
        Assert.True(g(x, top + 1) > 0, "内側にも光る");
        for (int d = 1; d < 20; d++) Assert.True(g(x, top + d + 1) <= g(x, top + d) + 1);
        Assert.True(g(x, top + 40) <= 1);
        window.Close();
    }

    [AvaloniaFact]
    public void 面取りでは発光をBorderのBoxShadowでなく八角形の層で描き_面取りなしでは従来どおり()
    {
        var chrome = new FrameChrome();
        var window = new Window { Content = chrome };
        window.Show();
        Assert.Equal(0, chrome.Frame.BoxShadow.Count);
        Assert.True(chrome.OuterGlow.IsVisible && chrome.InnerGlow.IsVisible);
        Assert.True(chrome.OuterGlow.Shadows.Count > 0);
        chrome.Chamfer = 0;
        Assert.True(chrome.Frame.BoxShadow.Count > 0);
        Assert.False(chrome.OuterGlow.IsVisible || chrome.InnerGlow.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void 明滅の画像は静的な発光と同じ八角形の形()
    {
        PulseLayer? pulse = null;
        var (g, edge, margin, window) = Draw(c => c.Overlays.Children.Add(pulse = new PulseLayer { Chamfer = Chamfer }));
        pulse!.Refresh();
        var baked = Read(pulse.BakedImage!);
        // 画像は Frame の外形 + 余白。窓の座標から画像の座標へは 余白（FrameChrome の Margin）を引いて余白を足す = そのまま
        int top = (int)Math.Floor(edge), mid = (int)(edge + W / 2.0);
        foreach (var (x, y) in new[] { (mid, top - 2), (mid, top - 7), (mid, top - 13), (top, top), (mid, top + 3) })
            AssertNear(g(x, y), baked(x, y));
        window.Close();
    }
}
