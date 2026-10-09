using System.Text.Json;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

/// <summary>グリッチの強さ（spec/EFFECTS.md「強さの倍率」、SETTINGS.md 演出タブ）。</summary>
public class GlitchLookTests
{
    [Fact]
    public void 強さの倍率はずれと影の濃さに比例し_濃さは1で止まる()
    {
        Assert.Equal(GlitchLook.Base, GlitchLook.Scaled(100));
        Assert.Equal(new GlitchLook(5, 0.55, 6, 16, 6), GlitchLook.Base);
        var half = GlitchLook.Scaled(50);
        Assert.Equal((2.5, 0.275, 3.0, 8.0, 3.0), (half.ShadowOffset, half.ShadowOpacity, half.BandShiftMin, half.BandShiftMax, half.FinalShift));
        var max = GlitchLook.Scaled(200);
        Assert.Equal((10.0, 1.0, 12.0, 32.0, 12.0), (max.ShadowOffset, max.ShadowOpacity, max.BandShiftMin, max.BandShiftMax, max.FinalShift));
        Assert.True(GlitchLook.Scaled(0).IsStill);
        Assert.False(GlitchLook.Scaled(10).IsStill);
        Assert.Equal(GlitchLook.Scaled(0), GlitchLook.Scaled(-10));
        Assert.Equal(GlitchLook.Scaled(200), GlitchLook.Scaled(300));
    }

    [Theory]
    [InlineData(250, 200)]
    [InlineData(-10, 0)]
    [InlineData(95, 100)]
    [InlineData(94, 90)]
    [InlineData(0, 0)]
    [InlineData(200, 200)]
    public void 読み込み時に0から200の10刻みへ丸める(int saved, int expected)
    {
        var s = new AppSettings();
        s.Appearance.GlitchIntensity = saved;
        s.Normalize();
        Assert.Equal(expected, s.Appearance.GlitchIntensity);
    }

    [Fact]
    public void キーが無ければ既定の100_往復で値が保たれる()
    {
        var empty = JsonSerializer.Deserialize<AppSettings>("{}", JsonDefaults.Options)!;
        Assert.Equal(100, empty.Appearance.GlitchIntensity);
        var s = new AppSettings();
        s.Appearance.GlitchIntensity = 150;
        string json = JsonSerializer.Serialize(s, JsonDefaults.Options);
        Assert.Contains("\"glitchIntensity\": 150", json);
        Assert.Equal(150, JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)!.Appearance.GlitchIntensity);
    }
}
