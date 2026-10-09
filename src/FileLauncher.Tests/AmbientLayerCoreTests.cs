using FileLauncher.Core.Effects;
using FileLauncher.Core.Theming;

namespace FileLauncher.Tests;

/// <summary>常時の演出の見た目の定数と配置の数式（issue/CA_AMBIENT.md §2）。Avalonia の層と Core Animation の層が共有する。</summary>
public class AmbientLayerCoreTests
{
    [Fact]
    public void 玉の経路の内側への距離は面取りありで太さの1_5倍_なしで半分()
    {
        Assert.Equal(2.25, AmbientLayout.OrbInset(10, 1.5));
        Assert.Equal(0.75, AmbientLayout.OrbInset(0, 1.5));
    }

    [Fact]
    public void 玉の経路は枠線の中心線の八角形で_周長はOrbPathと一致する()
    {
        var p = AmbientLayout.OrbCenterline(400, 300, 10, 1.5);
        Assert.Equal(8, p.Length);
        Assert.Equal((2.25 + 10, 2.25), p[0]);
        Assert.Equal(OrbPath.Octagon(400 - 4.5, 300 - 4.5, 10).Length, AmbientLayout.Perimeter(p), 9);
    }

    [Fact]
    public void CAのtimeOffsetは2個目で半周_尾は遅れの分だけ戻り_常に周期の中()
    {
        double p = 1000;
        Assert.Equal(0, AmbientLayout.OrbTimeOffsetMs(8000, 0, 0, p));
        Assert.Equal(4000, AmbientLayout.OrbTimeOffsetMs(8000, 0.5, 0, p));
        Assert.Equal(6000, AmbientLayout.OrbTimeOffsetMs(8000, 0, p / 4, p), 9);
        Assert.Equal(0, AmbientLayout.OrbTimeOffsetMs(8000, 0, p, p), 9);
        Assert.Equal(2000, AmbientLayout.OrbTimeOffsetMs(8000, 0.5, p / 4, p), 9);
        for (double lag = 0; lag < 3000; lag += 37)
        {
            double t = AmbientLayout.OrbTimeOffsetMs(8000, 0.5, lag, p);
            Assert.InRange(t, 0, 7999.999999);
        }
        Assert.Equal(0, AmbientLayout.OrbTimeOffsetMs(8000, 0, 10, 0));
    }

    [Fact]
    public void 明滅のキーフレームは0から始まり中央でpeak_最後に0へ戻る()
    {
        var k = AmbientLook.PulseKeyframes(0.45, 33);
        Assert.Equal(33, k.Length);
        Assert.Equal(0, k[0]);
        Assert.Equal(0, k[32], 9);
        Assert.Equal(0.45, k[16], 9);
        Assert.Equal(k[8], k[24], 9);
        for (int i = 1; i <= 16; i++) Assert.True(k[i] > k[i - 1]);
    }

    [Fact]
    public void 明滅の種類からpeak()
    {
        Assert.Equal(0.45, AmbientLook.PulsePeak(EffectKind.Pulse));
        Assert.Equal(0.9, AmbientLook.PulsePeak(EffectKind.PulseStrong));
        Assert.Equal(0, AmbientLook.PulsePeak(EffectKind.None));
        Assert.Equal(1.9, AmbientLook.MaxPulseGain, 9);
    }

    [Fact]
    public void 帯の中心は面の上の外から下の外まで動く()
    {
        Assert.Equal(-30, AmbientLayout.BeamCenterY(0, 330));
        Assert.Equal(360, AmbientLayout.BeamCenterY(1, 330));
        Assert.Equal(165, AmbientLayout.BeamCenterY(0.5, 330));
    }

    [Fact]
    public void 尾は10個で先端から並び_芯の色は主色を白と60パーセント混ぜる()
    {
        var steps = AmbientLook.OrbTailSteps();
        Assert.Equal(10, steps.Length);
        Assert.Equal((3, 0, 48), steps[0]);
        Assert.Equal(6.6, steps[9].Radius, 9);
        Assert.Equal(0.54, steps[9].Opacity, 9);
        Assert.Equal(4.8, steps[9].LagPx, 9);
        Assert.Equal(new RgbColor(153, 255, 255), AmbientLook.OrbCoreColor(new RgbColor(0, 255, 255)));
    }

    [Fact]
    public void 面と明滅の画像の置き場所()
    {
        Assert.Equal((41.5, 41.5, 317, 217), AmbientLayout.FaceRect(40, 40, 320, 220, 1.5));
        Assert.Equal((0, 0, 400, 300), AmbientLayout.PulseLayerRect(40, 40, 40, 800, 600, 2));
    }
}
