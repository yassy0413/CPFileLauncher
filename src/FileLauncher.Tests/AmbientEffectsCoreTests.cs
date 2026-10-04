using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public sealed class AmbientEffectsCoreTests
{
    [Fact]
    public void 角丸矩形の周長と_周に沿った位置が時計回りに進む()
    {
        var path = new OrbPath(100, 60, 10);
        Assert.Equal(2 * (80 + 40) + 2 * Math.PI * 10, path.Length, 6);
        Assert.Equal((10, 0), path.PointAt(0));
        Assert.Equal((50, 0), path.PointAt(40));                       // 上辺の途中
        var corner = path.PointAt(80 + Math.PI * 10 / 4);               // 右上の円弧の真ん中
        Assert.Equal(90 + 10 * Math.Cos(-Math.PI / 4), corner.X, 6);
        Assert.Equal(10 + 10 * Math.Sin(-Math.PI / 4), corner.Y, 6);
        var right = path.PointAt(80 + Math.PI * 5 + 20);                // 右辺の途中
        Assert.Equal(100, right.X, 6);
        Assert.Equal(30, right.Y, 6);
        Assert.Equal(path.PointAt(5), path.PointAt(5 + path.Length));  // 周長で巻く
        Assert.Equal(path.PointAt(path.Length - 5), path.PointAt(-5)); // 負も可
    }

    [Fact]
    public void 大きさ0や角丸が大きすぎても例外にならない()
    {
        Assert.Equal((0, 0), new OrbPath(0, 0, 8).PointAt(3));
        var round = new OrbPath(20, 20, 50); // 角丸は短辺の半分に丸める = 円
        Assert.Equal(2 * Math.PI * 10, round.Length, 6);
    }

    [Fact]
    public void 明滅は0から始まり半周期で最大になる()
    {
        Assert.Equal(0, AmbientMath.PulseLevel(0, 4000, 0.45), 6);
        Assert.Equal(0.45, AmbientMath.PulseLevel(2000, 4000, 0.45), 6);
        Assert.Equal(0.225, AmbientMath.PulseLevel(1000, 4000, 0.45), 6);
        Assert.Equal(0.125, AmbientMath.Phase(9000, 8000), 6);
        Assert.Equal(0, AmbientMath.Phase(100, 0));
    }

    [Fact]
    public void 常時の演出はどのテーマでも既定で動かず_時間は演出ごとの範囲に丸める()
    {
        Assert.Equal(EffectKind.None, EffectCatalog.DefaultFor(EffectCatalog.FrameOrb).Kind); // CPU を多く使うので既定なし
        Assert.Equal(EffectKind.None, EffectCatalog.DefaultFor(EffectCatalog.GlowPulse).Kind); // 重いので既定なし

        var n = EffectCatalog.Normalize(new()
        {
            [EffectCatalog.FrameOrb] = new EffectSpec { Kind = EffectKind.OrbTwin, DurationMs = 50000, Easing = EasingKind.EaseOut },
            [EffectCatalog.GlowPulse] = new EffectSpec { Kind = EffectKind.Pulse, DurationMs = 10 },
            [EffectCatalog.BoardShow] = new EffectSpec { Kind = EffectKind.Orb, DurationMs = 100 }, // 選べない種類は捨てる
        });
        Assert.Equal(new EffectSpec { Kind = EffectKind.OrbTwin, DurationMs = 20000, Easing = EasingKind.Linear }, n[EffectCatalog.FrameOrb]);
        Assert.Equal(1000, n[EffectCatalog.GlowPulse].DurationMs);
        Assert.False(n.ContainsKey(EffectCatalog.BoardShow));
    }
}
