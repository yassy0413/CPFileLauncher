using FileLauncher.Core.Effects;
using FileLauncher.Core.Theming;

namespace FileLauncher.Tests;

/// <summary>面取りに沿う枠の発光の Core（issue/CHAMFER_GLOW.md ステップ 1、SPEC §3.9「面取りの仕様」）。</summary>
public class NeonProfileTests
{
    // FlBoardGlow の外側 3 層（Neon(near 4, mid 14, far 32)。不透明度 0xE6 / 0x8C / 0x4D）
    private static readonly NeonLayer[] Board = [new(4, 0xE6 / 255.0), new(14, 0x8C / 255.0), new(32, 0x4D / 255.0)];

    [Fact]
    public void Erfcは0で1_3で十分小さく_単調に減る()
    {
        Assert.Equal(1, NeonProfile.Erfc(0), 6);
        Assert.True(NeonProfile.Erfc(3) < 1e-4);
        Assert.Equal(2 - NeonProfile.Erfc(0.7), NeonProfile.Erfc(-0.7), 9);
        for (double x = 0; x < 4; x += 0.1) Assert.True(NeonProfile.Erfc(x + 0.1) < NeonProfile.Erfc(x));
    }

    [Fact]
    public void σはAvaloniaのBoxShadowと同じ換算()
    {
        Assert.Equal(9.7376, NeonProfile.Sigma(32), 3);
        Assert.Equal(0, NeonProfile.Sigma(0));
    }

    [Fact]
    public void 合成は辺で各層の半分を重ねた値で_外へ単調に減る()
    {
        double expected = 1 - Board.Aggregate(1.0, (p, l) => p * (1 - l.Alpha / 2));
        Assert.Equal(expected, NeonProfile.Composite(Board, 0), 6);
        for (double d = 0; d < 40; d += 0.5) Assert.True(NeonProfile.Composite(Board, d + 0.5) <= NeonProfile.Composite(Board, d));
    }

    [Fact]
    public void 盤面の発光は明滅の強で明るくしても30px以内で消えきる()
    {
        double plain = NeonProfile.Extent(Board), strong = NeonProfile.Extent(Board, 1.9);
        Assert.InRange(strong, 24, 30);
        Assert.True(plain <= strong);
        Assert.True(NeonProfile.Composite(Board, strong) * 1.9 < NeonProfile.Invisible);
    }

    [Fact]
    public void 帯は外側から内側へ隙間なく並び_帯の中央の合成で塗る()
    {
        var bands = NeonProfile.Bands(Board, 1.9);
        Assert.Equal(NeonProfile.Extent(Board, 1.9), bands[0].Outer);
        Assert.Equal(0, bands[^1].Inner);
        for (int i = 0; i < bands.Count; i++)
        {
            var (outer, inner, alpha) = bands[i];
            Assert.Equal(NeonProfile.Step, outer - inner, 9);
            if (i > 0) Assert.Equal(bands[i - 1].Inner, outer);
            Assert.Equal(NeonProfile.Composite(Board, (outer + inner) / 2), alpha, 9);
        }
    }

    [Fact]
    public void 八角形は8点で上辺の始まりから時計回り_面取りは辺の半分に丸める()
    {
        var p = Octagon.Points(1, 2, 100, 60, 10);
        Assert.Equal(8, p.Length);
        Assert.Equal((11.0, 2.0), p[0]);
        Assert.Equal((91.0, 2.0), p[1]);
        Assert.Equal((101.0, 12.0), p[2]);
        Assert.Equal((1.0, 12.0), p[7]);
        // 符号付き面積が正 = 画面座標（y 下向き）で時計回り
        double area = 0;
        for (int i = 0; i < 8; i++) area += p[i].X * p[(i + 1) % 8].Y - p[(i + 1) % 8].X * p[i].Y;
        Assert.True(area > 0);
        Assert.Equal((30.0, 0.0), Octagon.Points(0, 0, 100, 60, 99)[0]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    [InlineData(-4)]
    public void オフセットした八角形の斜辺は元の斜辺から距離dだけ離れる(double d)
    {
        const double c = 10;
        // 左上の斜辺は直線 x + y = c（矩形の左上を原点）。オフセット後は矩形が d 広がり x + y = −2d + c'
        double offsetLine = -2 * d + Octagon.OffsetChamfer(c, d);
        Assert.Equal(d, (c - offsetLine) / Math.Sqrt(2), 9);
    }

    [Fact]
    public void 光の玉の八角形の経路は枠の辺の上を時計回りに一周する()
    {
        const double w = 200, h = 120, c = 10;
        var path = OrbPath.Octagon(w, h, c);
        Assert.Equal(2 * (w + h) - 8 * c + 4 * c * Math.Sqrt(2), path.Length, 9);
        Assert.Equal((c, 0.0), path.PointAt(0));
        var mid = path.PointAt(w - 2 * c + c * Math.Sqrt(2) / 2); // 右上の斜辺の中点
        Assert.Equal(w - c, mid.X - mid.Y, 9); // 右上の斜辺は直線 x − y = w − c
        Assert.Equal(path.PointAt(0), path.PointAt(path.Length));

        var p = Octagon.Points(0, 0, w, h, c);
        for (double s = 0; s < path.Length; s += 3.7)
        {
            var (x, y) = path.PointAt(s);
            double best = double.MaxValue;
            for (int i = 0; i < 8; i++) best = Math.Min(best, SegmentDistance(x, y, p[i], p[(i + 1) % 8]));
            Assert.True(best < 1e-9, $"{s}: ({x}, {y}) は辺から {best}");
        }
    }

    private static double SegmentDistance(double x, double y, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        double px = a.X + t * dx - x, py = a.Y + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }
}
