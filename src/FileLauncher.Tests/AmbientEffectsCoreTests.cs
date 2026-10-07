using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

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
    public void 常時の演出3つは既定で動き_時間は演出ごとの範囲に丸める()
    {
        // 3 つともこのアプリの肝なので既定 ON（2026-10-07 ユーザー判断）
        Assert.Equal(new EffectSpec { Kind = EffectKind.Orb, DurationMs = 8000, Easing = EasingKind.Linear }, EffectCatalog.DefaultFor(EffectCatalog.FrameOrb));
        Assert.Equal(new EffectSpec { Kind = EffectKind.Pulse, DurationMs = 4000, Easing = EasingKind.Linear }, EffectCatalog.DefaultFor(EffectCatalog.GlowPulse));
        Assert.Empty(EffectCatalog.Normalize(new()
        {
            [EffectCatalog.FrameOrb] = new EffectSpec { Kind = EffectKind.Orb, DurationMs = 8000 },
            [EffectCatalog.GlowPulse] = new EffectSpec { Kind = EffectKind.Pulse, DurationMs = 4000 },
        }));
        Assert.Equal(new EffectSpec { Kind = EffectKind.Beam, DurationMs = 4000, Easing = EasingKind.Linear }, EffectCatalog.DefaultFor(EffectCatalog.ScanBeam));
        var beam = EffectCatalog.Normalize(new()
        {
            [EffectCatalog.ScanBeam] = new EffectSpec { Kind = EffectKind.Beam, DurationMs = 4000 }, // 既定と同じは保存しない
        });
        Assert.Empty(beam);
        var off = EffectCatalog.Normalize(new() { [EffectCatalog.ScanBeam] = EffectSpec.None }); // 止めたことは保存する
        Assert.Equal(EffectKind.None, off[EffectCatalog.ScanBeam].Kind);
        Assert.Equal(12000, EffectCatalog.Normalize(new() { [EffectCatalog.ScanBeam] = new EffectSpec { Kind = EffectKind.Beam, DurationMs = 99999 } })[EffectCatalog.ScanBeam].DurationMs);

        var n = EffectCatalog.Normalize(new()
        {
            [EffectCatalog.FrameOrb] = new EffectSpec { Kind = EffectKind.OrbTwin, DurationMs = 50000, Easing = EasingKind.EaseOut },
            [EffectCatalog.GlowPulse] = new EffectSpec { Kind = EffectKind.Pulse, DurationMs = 10 },
            [EffectCatalog.BoardShow] = new EffectSpec { Kind = EffectKind.Orb, DurationMs = 100 }, // 選べない種類は捨てる
        });
        Assert.Equal(new EffectSpec { Kind = EffectKind.OrbTwin, DurationMs = 20000, Easing = EasingKind.Linear }, n[EffectCatalog.FrameOrb]);
        Assert.Equal(1000, n[EffectCatalog.GlowPulse].DurationMs);
        Assert.False(n.ContainsKey(EffectCatalog.BoardShow));
        Assert.False(EffectCatalog.Normalize(new() { [EffectCatalog.BoardShow] = new EffectSpec { Kind = EffectKind.Beam, DurationMs = 100 } }).ContainsKey(EffectCatalog.BoardShow));
    }

    [Fact]
    public void 走査線の帯は面の上の外から入り_下の外へ抜ける()
    {
        Assert.Equal(-60, AmbientMath.SweepOffset(0, 300, 60));
        Assert.Equal(120, AmbientMath.SweepOffset(0.5, 300, 60));
        Assert.Equal(300, AmbientMath.SweepOffset(1, 300, 60));
    }

    [Theory]
    [InlineData(53, 50)]
    [InlineData(12, 10)]
    [InlineData(13, 15)]
    [InlineData(-5, 0)]
    public void 走査線の濃さは0から50の5刻みに丸める(int raw, int expected)
    {
        var s = new AppSettings();
        s.Appearance.Hud.ScanlineOpacity = raw;
        s.Normalize();
        Assert.Equal(expected, s.Appearance.Hud.ScanlineOpacity);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(5, 3)]
    [InlineData(-3, 3)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void 走査線の間隔は2_3_4以外なら3に戻す(int raw, int expected)
    {
        var s = new AppSettings();
        s.Appearance.Hud.ScanlinePitch = raw;
        s.Normalize();
        Assert.Equal(expected, s.Appearance.Hud.ScanlinePitch);
    }

    [Fact]
    public void 走査線のキーが無い設定は既定のONと25パーセントと3pxになる()
    {
        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"appearance\": {\"hud\": {}}}", JsonDefaults.Options)!;
        Assert.Equal((true, 25, 3), (s.Appearance.Hud.Scanlines, s.Appearance.Hud.ScanlineOpacity, s.Appearance.Hud.ScanlinePitch));
    }

    [Theory]
    [InlineData(1, 13)]
    [InlineData(2, 30)]
    [InlineData(3, 47)]
    [InlineData(0, 13)]
    [InlineData(9, 47)]
    public void VSyncは60Hzの枚数ぶんより少し短い間隔で更新する(int vsync, double ms) =>
        Assert.Equal(ms, AmbientMath.FrameIntervalMs(vsync));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(7, 3)]
    public void VSyncは1から3に丸め_キーが無ければ2(int raw, int expected)
    {
        var s = new AppSettings();
        s.Appearance.Vsync = raw;
        s.Normalize();
        Assert.Equal(expected, s.Appearance.Vsync);
        Assert.Equal(2, System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"appearance\": {}}", JsonDefaults.Options)!.Appearance.Vsync);
    }
}
