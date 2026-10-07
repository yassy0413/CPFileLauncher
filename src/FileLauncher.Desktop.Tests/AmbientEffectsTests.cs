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
    private static (BoardWindow Board, AppearanceSettings Appearance) Board(bool orbOn = true, bool show = true, bool beamOn = true)
    {
        var appearance = new AppearanceSettings();
        appearance.Effects[EffectCatalog.BoardShow] = EffectSpec.None; // 表示演出の待ちを無くす
        if (!beamOn) appearance.Effects[EffectCatalog.ScanBeam] = EffectSpec.None; // 帯は既定 ON
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
    public void 既定では走査線の帯だけが動き_帯をなしにすると動かない()
    {
        var (board, appearance) = Board(orbOn: false);
        Assert.True(board.AmbientRunning); // 帯は既定 ON（2026-10-07 ユーザー判断）
        board.Ambient.Tick(0);
        Assert.True(board.ScanBeam.BandVisible);
        Assert.Empty(board.Orbs.OrbPositions);
        Assert.Equal(0, board.PulseLayer.Level);

        appearance.Effects[EffectCatalog.ScanBeam] = EffectSpec.None;
        board.ApplyEffects(appearance);
        Assert.False(board.AmbientRunning);
        Close(board);
    }

    [AvaloniaFact]
    public void 走査線の帯は面の上の外から下の外へ流れ_面の形で切り抜かれる()
    {
        var (board, _) = Board(orbOn: false);
        var beam = board.ScanBeam;
        double h = beam.Bounds.Height;
        board.Ambient.Tick(0);
        Assert.Equal(-ScanBeamLayer.BandHeight, beam.BandY, 3);
        board.Ambient.Tick(2000); // 4 秒で 1 回 → 半分
        Assert.Equal((h - ScanBeamLayer.BandHeight) / 2, beam.BandY, 3);
        var chrome = board.GetVisualDescendants().OfType<FrameChrome>().Single();
        Assert.NotNull(chrome.FaceClip);
        Assert.Same(chrome.FaceClip, beam.Clip);
        Assert.True(beam.Clip!.FillContains(new Avalonia.Point(h / 2, h / 2)));
        Assert.False(beam.Clip.FillContains(new Avalonia.Point(1, 1))); // 面取りで切り落とした角
        Close(board);
    }

    [AvaloniaFact]
    public void 既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる()
    {
        var (dark, _) = Board(orbOn: false, beamOn: false);
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
        Assert.InRange(y0, 2, 2.5); // 始点は上辺（面取りの枠線の中心 = Frame の枠 1.5 + 線の半分 0.75）

        board.Ambient.Tick(4000); // 8 秒で 1 周 → 半分
        var (x1, y1) = Assert.Single(orbs.OrbPositions);
        Assert.InRange(orbs.Bounds.Height - y1, 2, 2.5); // 半周で下辺

        board.Ambient.Tick(2000); // 明滅 4 秒周期 → 半周期で最大（弱 = 0.45）
        Assert.Equal(0.45, board.PulseLayer.Level, 2);
        Close(board);
    }

    [AvaloniaFact]
    public void 玉は面取りした角で斜辺の上を通る()
    {
        var (board, _) = Board();
        var orbs = board.Orbs;
        double inset = 2.25, c = FrameChrome.DefaultChamfer, w = orbs.Bounds.Width - inset * 2;
        double perimeter = 2 * (w + orbs.Bounds.Height - inset * 2) - 8 * c + 4 * c * Math.Sqrt(2);
        double ms = (w - 2 * c + c * Math.Sqrt(2) / 2) / perimeter * 8000; // 右上の斜辺の中点に着く時刻
        board.Ambient.Tick(0);
        board.Ambient.Tick(ms);
        var (x, y) = Assert.Single(orbs.OrbPositions);
        Assert.Equal(w - c, (x - inset) - (y - inset), 1); // 右上の斜辺 x − y = w − c の上
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
