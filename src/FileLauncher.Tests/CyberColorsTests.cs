using System.Text.Json;
using System.Text.Json.Nodes;
using FileLauncher.Core.Hud;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;
using FileLauncher.Core.Theming;

namespace FileLauncher.Tests;

public sealed class CyberColorsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static RgbColor C(string hex) => RgbColor.TryParse(hex)!.Value;

    [Fact]
    public void 色の文字列は6桁と8桁を読み_大文字の6桁で書く()
    {
        Assert.Equal(new RgbColor(0, 0xE5, 0xFF), RgbColor.TryParse("#00e5ff"));
        Assert.Equal(new RgbColor(0, 0xE5, 0xFF), RgbColor.TryParse("FF00E5FF")); // アルファは捨てる
        Assert.Null(RgbColor.TryParse("#12345"));
        Assert.Null(RgbColor.TryParse("blue"));
        Assert.Equal("#00E5FF", new RgbColor(0, 0xE5, 0xFF).ToHex());
    }

    [Fact]
    public void HSLとの往復とコントラスト比()
    {
        foreach (var hex in new[] { "#00E5FF", "#FF2E4D", "#808080", "#000000", "#FFFFFF" })
        {
            var c = C(hex);
            var (h, s, l) = ColorMath.ToHsl(c);
            var back = ColorMath.FromHsl(h, s, l);
            Assert.True(Math.Abs(back.R - c.R) <= 1 && Math.Abs(back.G - c.G) <= 1 && Math.Abs(back.B - c.B) <= 1, $"{hex} → {back}");
        }
        Assert.Equal(21, ColorMath.Contrast(C("#000000"), C("#FFFFFF")), 1);
        Assert.Equal(1, ColorMath.Contrast(C("#123456"), C("#123456")), 3);
        Assert.Equal(new RgbColor(128, 128, 128), ColorMath.Mix(C("#000000"), C("#FFFFFF"), 0.5) with { R = 128, G = 128, B = 128 });
    }

    [Fact]
    public void 背景と文字は主色の色相から導き_無彩色の主色なら無彩色()
    {
        var p = CyberDerivation.Derive(C("#00E5FF"), C("#FF2BD6"));
        var (hp, _, _) = ColorMath.ToHsl(C("#00E5FF"));
        var (hb, _, lb) = ColorMath.ToHsl(p.BoardBackground);
        Assert.InRange(Math.Abs(hb - hp), 0, 6);
        Assert.InRange(lb, 0.06, 0.1);
        Assert.True(ColorMath.Contrast(p.Text, p.BoardBackground) > 12);

        var gray = CyberDerivation.Derive(C("#9A9A9A"), C("#FF2BD6"));
        var (_, s, _) = ColorMath.ToHsl(gray.WindowBackground);
        Assert.Equal(0, s, 2);
    }

    [Fact]
    public void プリセットは2色の組で_一致すればそのID()
    {
        Assert.Equal(5, ColorPresets.All.Count);
        var (p, s) = ColorPresets.For(AccentPreset.Red);
        Assert.Equal(("#FF2E4D", "#37EBF3"), (p.ToHex(), s.ToHex()));
        Assert.Equal(AccentPreset.Red, ColorPresets.Match(p, s));
        Assert.Null(ColorPresets.Match(p, C("#000001")));
    }

    [Fact]
    public void 読めない色はシアンに戻し_暗い色はそのまま残す()
    {
        var c = new ColorSettings { Primary = "oops", Secondary = "#101010" };
        Assert.True(c.Normalize());
        Assert.Equal("#00E5FF", c.Primary);
        Assert.Equal("#101010", c.Secondary); // 自動で明るくしない（2026-10-04 ユーザー判断）

        var ok = new ColorSettings { Primary = "#00e5ff", Secondary = "#FF2BD6" };
        Assert.False(ok.Normalize());
        Assert.Equal("#00E5FF", ok.Primary); // 表記だけ大文字にそろえる
    }

    [Fact]
    public void 版2の設定はテーマを捨てて基調色を主色と副色に移す()
    {
        Directory.CreateDirectory(_dir);
        var paths = new DataPaths(_dir, IsPortable: false);
        File.WriteAllText(paths.SettingsFile, """
            { "schemaVersion": 2, "appearance": { "theme": "dark", "accent": "red", "opacity": 70 } }
            """);
        var s = new AppDataStore(paths).LoadAll().Settings.Value;
        Assert.Equal(3, s.SchemaVersion);
        Assert.Equal(("#FF2E4D", "#37EBF3"), (s.Appearance.Colors.Primary, s.Appearance.Colors.Secondary));
        Assert.Equal(70, s.Appearance.Opacity);
        string json = JsonSerializer.Serialize(s, JsonDefaults.Options);
        Assert.DoesNotContain("\"theme\"", json);
        Assert.DoesNotContain("\"accent\"", json);
        Assert.True(JsonNode.Parse(json)!["appearance"]!["hud"]!["statusBar"]!.GetValue<bool>());
    }

    [Fact]
    public void ステータス行の書式と_次に書き換える時刻()
    {
        var now = new DateTime(2026, 10, 4, 15, 5, 34, 250);
        Assert.Equal(("PAGE 02/05", "ITEMS 24", "15:05"), StatusLine.Format(1, 5, 24, now, ClockMode.Minutes));
        Assert.Equal("15:05:34", StatusLine.Format(0, 1, 0, now, ClockMode.Seconds).Clock);
        Assert.Null(StatusLine.Format(0, 1, 0, now, ClockMode.Off).Clock);
        Assert.Equal("PAGE 1/120", StatusLine.Format(0, 120, 0, now, ClockMode.Off).Page);
        Assert.Equal(TimeSpan.FromMilliseconds(25750), StatusLine.NextTick(now, ClockMode.Minutes));
        Assert.Equal(TimeSpan.FromMilliseconds(750), StatusLine.NextTick(now, ClockMode.Seconds));
        // 日付付き（2026-10-04 ユーザー要望）
        Assert.Equal("2026/10/04 15:05", StatusLine.Format(0, 1, 0, now, ClockMode.Minutes, date: true).Clock);
        Assert.Equal("2026/10/04", StatusLine.Format(0, 1, 0, now, ClockMode.Off, date: true).Clock);
        Assert.Equal(new DateTime(2026, 10, 5) - now, StatusLine.NextTick(now, ClockMode.Off, date: true));
    }
}
