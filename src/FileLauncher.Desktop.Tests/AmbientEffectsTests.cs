using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileLauncher.App;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Desktop.Tests;

/// <summary>常時の演出（光の玉 frameOrb・発光の明滅 glowPulse。spec/EFFECTS.md「常時の演出の詳細」）。</summary>
public sealed class AmbientEffectsTests
{
    private static (BoardWindow Board, AppearanceSettings Appearance) Board(bool orbOn = true, bool show = true)
    {
        var appearance = new AppearanceSettings();
        appearance.Effects[EffectCatalog.BoardShow] = EffectSpec.None; // 表示演出の待ちを無くす
        // 常時の演出は既定でオフなので、テストではオンにする（ダークは既定のまま = 動かないことを見る）
        if (orbOn) appearance.Effects[EffectCatalog.FrameOrb] = new EffectSpec { Kind = EffectKind.Orb, DurationMs = 8000, Easing = EasingKind.Linear };
        var board = new BoardWindow();
        board.ApplyEffects(appearance);
        board.Render(Core.Model.Board.CreateDefault(), appearance, 0);
        if (show)
        {
            board.Show();
            Dispatcher.UIThread.RunJobs();
        }
        return (board, appearance);
    }

    private static void Close(BoardWindow board)
    {
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void サイバーパンクで盤面が見えている間だけ動き_隠すと止まる()
    {
        var (board, _) = Board();
        Assert.True(board.AmbientRunning);
        board.Hide();
        Dispatcher.UIThread.RunJobs();
        Assert.False(board.AmbientRunning);
        board.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(board.AmbientRunning);
        Close(board);
    }

    [AvaloniaFact]
    public void 既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる()
    {
        var (dark, _) = Board(orbOn: false);
        Assert.False(dark.AmbientRunning);
        Close(dark);

        var (board, appearance) = Board();
        board.AmbientSuspended = true;
        Assert.False(board.AmbientRunning);
        board.AmbientSuspended = false;
        Assert.True(board.AmbientRunning);

        bool reduce = true;
        board.ReducedMotion = () => reduce;
        board.ApplyEffects(appearance);
        Assert.False(board.AmbientRunning);
        reduce = false;
        board.ApplyEffects(appearance);
        Assert.True(board.AmbientRunning);

        appearance.Animation = false;
        board.ApplyEffects(appearance);
        Assert.False(board.AmbientRunning);
        Close(board);
    }

    [AvaloniaFact]
    public void 玉は枠線に沿って一周の半分で反対側へ進み_明滅は半周期で最大になる()
    {
        var (board, appearance) = Board();
        appearance.Effects[EffectCatalog.GlowPulse] = new EffectSpec { Kind = EffectKind.Pulse, DurationMs = 4000, Easing = EasingKind.Linear };
        board.ApplyEffects(appearance);
        var orbs = board.Orbs;
        board.Ambient.Tick(0);
        var (x0, y0) = Assert.Single(orbs.OrbPositions);
        Assert.True(y0 < 2, $"始点は上辺: {x0},{y0}");

        board.Ambient.Tick(4000); // 8 秒で 1 周 → 半分
        var (x1, y1) = Assert.Single(orbs.OrbPositions);
        Assert.True(y1 > orbs.Bounds.Height - 2, $"半周で下辺: {x1},{y1}");

        board.Ambient.Tick(2000); // 明滅 4 秒周期 → 半周期で最大（弱 = 0.45）
        Assert.Equal(0.45, board.PulseLayer.Level, 2);
        Close(board);
    }

    [AvaloniaFact]
    public void 層の順は明滅と玉がグリッチとドラッグの層より下()
    {
        var (board, _) = Board();
        var overlays = board.GetVisualDescendants().OfType<FrameChrome>().Single().Overlays;
        var names = overlays.Children.Select(c => c is Grid g && g.Children.Contains(board.Orbs) ? "ambient" : c.Name).ToList();
        Assert.Equal(["ambient", "GlitchLayer", "DragLayer"], names);
        Close(board);
    }

    [AvaloniaFact]
    public void 設定画面に光の玉と発光の明滅の行と注記がある()
    {
        string dir = Path.Combine(Path.GetTempPath(), "FileLauncherUiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var paths = new DataPaths(dir, IsPortable: false);
            var store = new AppDataStore(paths);
            var hub = new SettingsHub(store, store.LoadAll().Settings.Value);
            var w = new SettingsWindow(new SettingsContext(hub, new FakePlatform(), paths, () => null, _ => { }, () => { }));
            w.Show();
            var tabs = w.GetVisualDescendants().OfType<TabControl>().First();
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == Strings.Settings_Tab_Appearance);
            Dispatcher.UIThread.RunJobs();
            w.GetVisualDescendants().OfType<Expander>().First().IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            string pulseRow = Strings.FormatSettings_Appearance_EffectRow(EnumNames.Effect(EffectCatalog.GlowPulse), EffectCatalog.GlowPulse);
            bool Visible() => w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == pulseRow && t.IsEffectivelyVisible);
            Assert.True(Visible());
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == Strings.Settings_Appearance_Effects_AmbientNote);
            w.Close();
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }
}
