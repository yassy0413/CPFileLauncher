using FileLauncher.Core.Model;

namespace FileLauncher.Core.Theming;

/// <summary>配色のプリセット（主色・副色の組。SPEC §3.6「配色」）。</summary>
public static class ColorPresets
{
    private static RgbColor C(string hex) => RgbColor.TryParse(hex)!.Value;

    public static IReadOnlyList<(AccentPreset Id, RgbColor Primary, RgbColor Secondary)> All { get; } =
    [
        (AccentPreset.Cyan, C("#00E5FF"), C("#FF2BD6")),
        (AccentPreset.Red, C("#FF2E4D"), C("#37EBF3")),
        (AccentPreset.Blue, C("#4D8BFF"), C("#C77DFF")),
        (AccentPreset.Green, C("#39FF88"), C("#FF4FD8")),
        (AccentPreset.Purple, C("#B967FF"), C("#00E5FF")),
    ];

    public static (RgbColor Primary, RgbColor Secondary) For(AccentPreset id)
    {
        foreach (var p in All) if (p.Id == id) return (p.Primary, p.Secondary);
        return (All[0].Primary, All[0].Secondary);
    }

    /// <summary>2 色がどれかのプリセットと完全に一致すればその ID（設定画面の「配色」の表示に使う）。</summary>
    public static AccentPreset? Match(RgbColor primary, RgbColor secondary)
    {
        foreach (var p in All) if (p.Primary == primary && p.Secondary == secondary) return p.Id;
        return null;
    }
}
