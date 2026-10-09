using System.Text.Json;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public class EffectTests
{
    private static EffectSpec Spec(EffectKind k, int ms, EasingKind e) => new() { Kind = k, DurationMs = ms, Easing = e };

    [Fact]
    public void 上書きが無ければ既定値_あればその値を使う()
    {
        var a = new AppearanceSettings();
        Assert.Equal(Spec(EffectKind.Glitch, 250, EasingKind.Linear), EffectCatalog.Resolve(a, EffectCatalog.BoardShow));

        a.Effects[EffectCatalog.BoardShow] = Spec(EffectKind.Zoom, 100, EasingKind.BackOut);
        Assert.Equal(Spec(EffectKind.Zoom, 100, EasingKind.BackOut), EffectCatalog.Resolve(a, EffectCatalog.BoardShow));
    }

    [Fact]
    public void アニメーションOFFなら全部なし()
    {
        var a = new AppearanceSettings { Animation = false };
        a.Effects[EffectCatalog.BoardShow] = Spec(EffectKind.Zoom, 100, EasingKind.BackOut);

        Assert.All(EffectCatalog.All, d => Assert.False(EffectCatalog.Resolve(a, d.Id).IsActive));
    }

    [Fact]
    public void 全演出のIDは重複せず_既定の種類はその演出で選べる種類に含まれる()
    {
        Assert.Equal(EffectCatalog.All.Count, EffectCatalog.All.Select(d => d.Id).Distinct().Count());
        Assert.All(EffectCatalog.All, d => Assert.Contains(d.Default.Kind, d.AllowedKinds));
    }

    [Fact]
    public void 正規化で時間を丸め_未知のIDと選べない種類と既定と同じ値を捨てる()
    {
        var s = new AppSettings();
        s.Appearance.Effects = new()
        {
            [EffectCatalog.BoardShow] = Spec(EffectKind.Zoom, 5000, EasingKind.EaseOut),
            [EffectCatalog.BoardHide] = Spec(EffectKind.Fade, -10, EasingKind.EaseIn),
            ["unknown"] = Spec(EffectKind.Fade, 100, EasingKind.Linear),
            [EffectCatalog.ItemHover] = Spec(EffectKind.Slide, 100, EasingKind.Linear),   // ホバーは none / highlight / glow のみ
            [EffectCatalog.PageSwitch] = Spec(EffectKind.Fade, 80, EasingKind.EaseOut),  // 既定と同じ
        };

        s.Normalize();

        Assert.Equal(2, s.Appearance.Effects.Count);
        Assert.Equal(1000, s.Appearance.Effects[EffectCatalog.BoardShow].DurationMs);
        Assert.Equal(0, s.Appearance.Effects[EffectCatalog.BoardHide].DurationMs);
    }

    [Fact]
    public void 演出の上書きはJSONでIDごとのオブジェクトとして往復する()
    {
        var s = new AppSettings();
        s.Appearance.Effects[EffectCatalog.BoardShow] = Spec(EffectKind.Zoom, 100, EasingKind.BackOut);

        string json = JsonSerializer.Serialize(s, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)!;

        Assert.Contains("\"boardShow\": {", json);
        Assert.Contains("\"kind\": \"zoom\"", json);
        Assert.Contains("\"easing\": \"backOut\"", json);
        Assert.Equal(s.Appearance.Effects[EffectCatalog.BoardShow], back.Appearance.Effects[EffectCatalog.BoardShow]);
    }

    [Fact]
    public void 既定は盤面の表示と非表示がグリッチ_ホバーとタブとドロップ先が発光()
    {
        var a = new AppearanceSettings();
        Assert.Equal(Spec(EffectKind.Glitch, 250, EasingKind.Linear), EffectCatalog.Resolve(a, EffectCatalog.BoardShow));
        Assert.Equal(Spec(EffectKind.Glitch, 250, EasingKind.Linear), EffectCatalog.Resolve(a, EffectCatalog.BoardHide));
        Assert.Equal(EffectKind.Glow, EffectCatalog.Resolve(a, EffectCatalog.ItemHover).Kind);
        Assert.Equal(EffectKind.Glow, EffectCatalog.Resolve(a, EffectCatalog.TabHighlight).Kind);
        Assert.Equal(EffectKind.Glow, EffectCatalog.Resolve(a, EffectCatalog.DropTarget).Kind);
    }

    [Fact]
    public void 正規化は既定値と同じものだけ捨てる()
    {
        var s = new AppSettings();
        s.Appearance.Effects[EffectCatalog.BoardShow] = Spec(EffectKind.Glitch, 250, EasingKind.Linear); // 既定と同じ
        s.Appearance.Effects[EffectCatalog.BoardHide] = Spec(EffectKind.Fade, 80, EasingKind.EaseIn);    // 既定と違う
        s.Normalize();
        Assert.False(s.Appearance.Effects.ContainsKey(EffectCatalog.BoardShow));
        Assert.True(s.Appearance.Effects.ContainsKey(EffectCatalog.BoardHide));
    }

    [Fact]
    public void 選べない種類は捨て_glitchのイージングは一定に揃える()
    {
        var s = new AppSettings();
        s.Appearance.Effects[EffectCatalog.ItemPress] = Spec(EffectKind.Glow, 60, EasingKind.Linear);
        s.Appearance.Effects[EffectCatalog.BoardShow] = Spec(EffectKind.Glitch, 200, EasingKind.BackOut);

        s.Normalize();

        Assert.False(s.Appearance.Effects.ContainsKey(EffectCatalog.ItemPress));
        Assert.Equal(Spec(EffectKind.Glitch, 200, EasingKind.Linear), s.Appearance.Effects[EffectCatalog.BoardShow]);
    }
}

public class AccentAndItemColorModelTests
{
    [Fact]
    public void 配色の既定はシアンのプリセット_アイテムの色は省略可能でJSONに名前で入る()
    {
        Assert.Equal(("#00E5FF", "#FF2BD6"), (new AppSettings().Appearance.Colors.Primary, new AppSettings().Appearance.Colors.Secondary));

        var item = new LauncherItem { Name = "x", Color = ItemColor.Purple };
        string json = JsonSerializer.Serialize(item, JsonDefaults.Options);
        Assert.Contains("\"color\": \"purple\"", json);
        Assert.Equal(ItemColor.Purple, JsonSerializer.Deserialize<LauncherItem>(json, JsonDefaults.Options)!.Color);
        Assert.Null(JsonSerializer.Deserialize<LauncherItem>("{\"name\": \"old\"}", JsonDefaults.Options)!.Color); // 旧データ
    }
}
