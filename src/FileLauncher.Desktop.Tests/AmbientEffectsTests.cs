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
    /// <summary>常時の演出は 3 つとも既定 ON なので、テストでは使うものだけを明示的に選ぶ（使わないものは none）。</summary>
    private static (BoardWindow Board, AppearanceSettings Appearance) Board(bool orbOn = true, bool show = true, bool beamOn = true, bool pulseOn = false)
    {
        var appearance = new AppearanceSettings();
        appearance.Effects[EffectCatalog.BoardShow] = EffectSpec.None; // 表示演出の待ちを無くす
        if (!beamOn) appearance.Effects[EffectCatalog.ScanBeam] = EffectSpec.None;
        if (!pulseOn) appearance.Effects[EffectCatalog.GlowPulse] = EffectSpec.None;
        appearance.Effects[EffectCatalog.FrameOrb] = orbOn ? new EffectSpec { Kind = EffectKind.Orb, DurationMs = 8000, Easing = EasingKind.Linear } : EffectSpec.None;
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
    public void 既定では3つとも動き_すべてなしにすると動かない()
    {
        var appearance = new AppearanceSettings();
        appearance.Effects[EffectCatalog.BoardShow] = EffectSpec.None;
        var board = new BoardWindow();
        board.ApplyEffects(appearance);
        board.Render(Core.Model.Board.CreateDefault(), appearance, 0);
        board.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(board.AmbientRunning); // 3 つとも既定 ON（2026-10-07 ユーザー判断）
        board.Ambient.Tick(0);
        board.Ambient.Tick(2000);
        Assert.True(board.ScanBeam.BandVisible);
        Assert.Single(board.Orbs.OrbPositions); // 玉 1 個
        Assert.True(board.PulseLayer.Level > 0); // 明滅「弱」
        Assert.Equal(2, board.Ambient.Vsync);

        foreach (var id in new[] { EffectCatalog.FrameOrb, EffectCatalog.GlowPulse, EffectCatalog.ScanBeam }) appearance.Effects[id] = EffectSpec.None;
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
    public void 三つなしなら動かず_アニメーションOFFや収納中やOSの設定でも止まる()
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
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == Strings.Settings_Tab_Effects);
            Dispatcher.UIThread.RunJobs();
            // 常時の演出 3 つは折りたたまずに見えている（ID は添えない）。残りは「詳細設定」の中
            var expander = w.GetVisualDescendants().OfType<Expander>().Single(e => (e.Header as string) == Strings.Settings_Effects_Advanced);
            Assert.False(expander.IsExpanded);
            foreach (var id in new[] { EffectCatalog.FrameOrb, EffectCatalog.GlowPulse, EffectCatalog.ScanBeam })
                Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == EnumNames.Effect(id) && t.IsEffectivelyVisible);
            string showRow = Strings.FormatSettings_Effects_Row(EnumNames.Effect(EffectCatalog.BoardShow), EffectCatalog.BoardShow);
            Assert.DoesNotContain(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == showRow && t.IsEffectivelyVisible);
            Assert.Contains(w.GetVisualDescendants().OfType<ComboBox>(), c => c.ItemsSource is IEnumerable<string> items && items.Contains(Strings.FormatSettings_Effects_Vsync_Item(1, 60)));
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == Strings.Settings_Effects_AmbientNote);
            w.Close();
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }

    [Fact]
    public void VSync2は約30fpsで更新し_最初のフレームは必ず更新する()
    {
        double min = AmbientMath.FrameIntervalMs(2);
        Assert.Equal(30, min);
        Assert.True(AmbientAnimator.ShouldTick(double.NegativeInfinity, 5, min));
        Assert.False(AmbientAnimator.ShouldTick(100, 129, min));
        Assert.True(AmbientAnimator.ShouldTick(100, 133.3, min)); // 60 Hz の 2 フレームごと
        Assert.True(AmbientAnimator.ShouldTick(100, 116.7, AmbientMath.FrameIntervalMs(1))); // VSync 1 = 毎フレーム（60 Hz）
        Assert.False(AmbientAnimator.ShouldTick(100, 108.3, AmbientMath.FrameIntervalMs(1))); // 120 Hz でも 60 fps
        Assert.True(AmbientAnimator.ShouldTick(100, 150, AmbientMath.FrameIntervalMs(3))); // 3 フレームごと
    }
}
