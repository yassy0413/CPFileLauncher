using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileLauncher.App;
using FileLauncher.Core.Model;

namespace FileLauncher.Desktop.Tests;

/// <summary>HUD 段階 A: ステータス行と空きスロットの角マーカー（SPEC §3.9）。</summary>
public sealed class HudTests
{
    private static BoardWindow Board(AppearanceSettings appearance, Board? data = null)
    {
        var board = new BoardWindow();
        board.ApplyEffects(appearance);
        board.Render(data ?? Core.Model.Board.CreateDefault(), appearance, 0);
        return board;
    }

    private static string Text(BoardWindow b, string name) => b.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text ?? "";

    [AvaloniaFact]
    public void ステータス行にページとアイテム数と時刻が出て_時刻は見えている間だけ動く()
    {
        var data = Core.Model.Board.CreateDefault();
        data.Pages.Add(new Page { Name = "P2", Rows = 2, Cols = 3 });
        data.Pages[0].Items.Add(new LauncherItem { Row = 0, Col = 0, Name = "a", Target = "/a" });
        var board = Board(new AppearanceSettings(), data);
        Assert.False(board.ClockRunning); // まだ出していない

        board.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("PAGE 01/02", Text(board, "StatusPage"));
        Assert.Equal("ITEMS 1", Text(board, "StatusItems"));
        Assert.Matches(@"^\d{4}/\d\d/\d\d \d\d:\d\d$", Text(board, "StatusClock")); // 既定は日付付き
        Assert.True(board.ClockRunning);

        board.SwitchPageTo(1);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("PAGE 02/02", Text(board, "StatusPage"));
        Assert.Equal("ITEMS 0", Text(board, "StatusItems"));

        board.Hide();
        Dispatcher.UIThread.RunJobs();
        Assert.False(board.ClockRunning);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void ステータス行を消すと盤面が低くなり_時刻なしならタイマーは動かない()
    {
        var on = new AppearanceSettings();
        var off = new AppearanceSettings { Hud = new HudSettings { StatusBar = false } };
        var noClock = new AppearanceSettings { Hud = new HudSettings { Clock = ClockMode.Off, Date = false } };
        var a = Board(on);
        var b = Board(off);
        Assert.Equal(19, a.Height - b.Height, 3);

        var c = Board(noClock);
        c.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(c.ClockRunning);
        Assert.Equal("", Text(c, "StatusClock"));
        foreach (var w in new[] { a, b, c }) { w.AllowClose = true; w.Close(); }
    }

    [AvaloniaFact]
    public void 空きスロットは四隅の角マーカーで描き_全周の枠線は描かない()
    {
        var board = Board(new AppearanceSettings());
        board.Show();
        Dispatcher.UIThread.RunJobs();
        var empty = board.GetVisualDescendants().OfType<Button>().First(x => x.Classes.Contains("empty"));
        var ticks = empty.GetVisualDescendants().OfType<CornerTicks>().Single();
        Assert.NotNull(ticks.Stroke);
        Assert.Equal(Avalonia.Media.Colors.Transparent, ((Avalonia.Media.ISolidColorBrush)empty.BorderBrush!).Color);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 背景グリッドは設定で出し入れできる()
    {
        var on = Board(new AppearanceSettings());
        var off = Board(new AppearanceSettings { Hud = new HudSettings { Grid = false } });
        on.Show(); off.Show();
        Dispatcher.UIThread.RunJobs();
        var layer = on.GetVisualDescendants().OfType<GridLayer>().Single();
        Assert.True(layer.IsVisible);
        Assert.Equal(0.03, layer.Opacity, 3); // 既定 3%
        Assert.False(off.GetVisualDescendants().OfType<GridLayer>().Single().IsVisible);
        foreach (var w in new[] { on, off }) { w.AllowClose = true; w.Close(); }
    }

    [AvaloniaFact]
    public void 走査線は既定で25パーセント3px間隔で出て_設定で消せて間隔も変わる()
    {
        var on = Board(new AppearanceSettings());
        var off = Board(new AppearanceSettings { Hud = new HudSettings { Scanlines = false } });
        var wide = Board(new AppearanceSettings { Hud = new HudSettings { ScanlinePitch = 4, ScanlineOpacity = 10 } });
        foreach (var w in new[] { on, off, wide }) w.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(on.ScanlineLayer.IsVisible);
        Assert.Equal(0.25, on.ScanlineLayer.Opacity, 3);
        Assert.Equal(3, on.ScanlineLayer.Pitch);
        Assert.False(off.ScanlineLayer.IsVisible);
        Assert.Equal((4.0, 0.1), (wide.ScanlineLayer.Pitch, Math.Round(wide.ScanlineLayer.Opacity, 3)));
        // グリッドの上・タブとスロットの下
        var surface = (Grid)on.ScanlineLayer.Parent!;
        Assert.Equal(surface.Children.IndexOf(surface.Children.OfType<GridLayer>().Single()) + 1, surface.Children.IndexOf(on.ScanlineLayer));
        Assert.True(surface.Children.IndexOf(on.ScanlineLayer) < surface.Children.IndexOf(surface.Children.OfType<DockPanel>().Single()));
        foreach (var w in new[] { on, off, wide }) { w.AllowClose = true; w.Close(); }
    }
}
