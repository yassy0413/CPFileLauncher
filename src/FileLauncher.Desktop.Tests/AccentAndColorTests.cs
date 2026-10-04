using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileLauncher.App;
using FileLauncher.Core.Model;
using FileLauncher.Core.Theming;

namespace FileLauncher.Desktop.Tests;

public sealed class AccentAndColorTests
{
    private static void SetPreset(Application app, AccentPreset id)
    {
        var (p, s) = ColorPresets.For(id);
        AppTheme.SetColors(app, p, s);
    }

    private static Color ColorOf(ResourceDictionary d, string key) => ((ISolidColorBrush)d[key]!).Color;

    /// <summary>
    /// Fluent のアクセント濃淡（Dark/Light 1〜3）は旧来の手決めの値と混色計算で各チャンネル最大 4 段階ずれる（見た目では分からない）ので近似で比べる。
    /// </summary>
    private static void AssertClose(string expected, object? actual)
    {
        var e = Color.Parse(expected);
        var a = (Color)actual!;
        Assert.True(Math.Abs(e.R - a.R) <= 4 && Math.Abs(e.G - a.G) <= 4 && Math.Abs(e.B - a.B) <= 4, $"{expected} ≉ {a}");
    }

    [AvaloniaFact]
    public void シアンの配色から導いたトークンはSPECの導出規則どおり()
    {
        var (p, s) = ColorPresets.For(AccentPreset.Cyan);
        var derived = CyberDerivation.Derive(p, s);
        var d = AppTheme.Cyber(FileLauncher.App.CyberPalette.From(derived));
        Assert.Equal(Color.Parse("#CC00E5FF"), ColorOf(d, "FlBoardBorder"));
        Assert.Equal(Color.Parse("#1F00E5FF"), ColorOf(d, "FlSlotHoverBackground"));
        Assert.Equal(Color.Parse("#24FF2BD6"), ColorOf(d, "FlDropBackground"));
        Assert.Equal(Color.Parse("#3300E5FF"), ColorOf(d, "FlEmptySlotBorder"));
        var wb = derived.WindowBackground;
        Assert.Equal(Color.FromArgb(0xF5, wb.R, wb.G, wb.B), ColorOf(d, "FlToastBackground")); // 窓の背景（主色から導出）に 96%
        Assert.Equal(Color.Parse("#9900E5FF"), ColorOf(d, "FlToastBorder"));
        AssertClose("#00B8CC", d["SystemAccentColorDark1"]);
        AssertClose("#008C99", d["SystemAccentColorDark2"]);
        AssertClose("#006066", d["SystemAccentColorDark3"]);
        AssertClose("#4DEDFF", d["SystemAccentColorLight1"]);
        AssertClose("#80F2FF", d["SystemAccentColorLight2"]);
        AssertClose("#B3F7FF", d["SystemAccentColorLight3"]);
        Assert.Equal(4, ((BoxShadows)d["FlBoardGlow"]!).Count); // ネオン管の 3 段 + 内側
        Assert.Equal(Color.Parse("#E600E5FF"), ((BoxShadows)d["FlBoardGlow"]!)[0].Color);
    }

    [AvaloniaFact]
    public void 配色を変えるとアクセントが変わり_戻せる()
    {
        var app = Application.Current!;
        try
        {
            SetPreset(app, AccentPreset.Red);
            Assert.True(app.TryGetResource("FlAccent", AppTheme.Cyberpunk, out var red));
            Assert.Equal(Color.Parse("#FF2E4D"), ((ISolidColorBrush)red!).Color);

            SetPreset(app, AccentPreset.Cyan);
            Assert.True(app.TryGetResource("FlAccent", AppTheme.Cyberpunk, out var cyan));
            Assert.Equal(Color.Parse("#00E5FF"), ((ISolidColorBrush)cyan!).Color);
        }
        finally { SetPreset(app, AccentPreset.Cyan); }
    }

    [AvaloniaFact]
    public void 盤面に表示中でも配色の変更が枠の色へすぐ反映される()
    {
        var app = Application.Current!;
        var board = new BoardWindow();
        try
        {
            var appearance = new AppearanceSettings();
            board.Render(Board.CreateDefault(), appearance, 0);
            board.Show();
            Dispatcher.UIThread.RunJobs();
            var chrome = board.GetVisualDescendants().OfType<FrameChrome>().Single();

            SetPreset(app, AccentPreset.Green);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(Color.Parse("#CC39FF88"), ((ISolidColorBrush)chrome.OutlineBrush!).Color);
        }
        finally
        {
            SetPreset(app, AccentPreset.Cyan);
            board.AllowClose = true;
            board.Close();
        }
    }

    [AvaloniaFact]
    public void 色を付けたアイテムだけ左端に色帯が出る()
    {
        var board = new BoardWindow();
        var b = Board.CreateDefault();
        b.Pages[0].Items.Add(new LauncherItem { Row = 0, Col = 0, Name = "red", Target = "/nope", Color = ItemColor.Red });
        b.Pages[0].Items.Add(new LauncherItem { Row = 0, Col = 1, Name = "plain", Target = "/nope" });
        board.Render(b, new AppearanceSettings(), 0);
        board.Show();
        Dispatcher.UIThread.RunJobs();

        // 色帯 = 左寄せ・幅 3 px・当たり判定なしの Border（欠落マークも赤いことがあるので形で見分ける）
        var bands = board.GetVisualDescendants().OfType<Border>()
            .Where(x => x.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Left && x.Width == 3 && !x.IsHitTestVisible)
            .ToList();
        var band = Assert.Single(bands);
        var expected = (ISolidColorBrush)Application.Current!.FindResource(board.ActualThemeVariant, AppTheme.ItemColorKey(ItemColor.Red))!;
        Assert.Equal(expected.Color, ((ISolidColorBrush)band.Background!).Color);
        board.AllowClose = true;
        board.Close();
    }
}
