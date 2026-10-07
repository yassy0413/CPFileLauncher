using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileLauncher.App;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Desktop.Tests;

public sealed class BackgroundTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherUiTests", Guid.NewGuid().ToString("N"));

    public BackgroundTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Png(string name, int w, int h)
    {
        string path = Path.Combine(_dir, name);
        using var bmp = new RenderTargetBitmap(new PixelSize(w, h));
        using (var ctx = bmp.CreateDrawingContext()) ctx.FillRectangle(Brushes.OrangeRed, new Rect(0, 0, w, h));
        bmp.Save(path);
        return path;
    }

    private static T Wait<T>(Task<T> task)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        return task.GetAwaiter().GetResult();
    }

    [AvaloniaFact]
    public void 小さい画像は原寸で_長辺が2048を超える画像は縮小して読む()
    {
        using var loader = new BoardBackgroundLoader();
        var small = Wait(loader.LoadAsync(Png("small.png", 300, 200)))!;
        Assert.Equal(new PixelSize(300, 200), small.PixelSize);
        Assert.Equal(BackgroundLoadStatus.Ok, loader.LastStatus);
        Assert.Same(small, Wait(loader.LoadAsync(Path.Combine(_dir, "small.png")))); // 同じファイルは読み直さない

        var tall = Wait(loader.LoadAsync(Png("tall.png", 1000, 3000)))!;
        Assert.Equal(2048, tall.PixelSize.Height);
        Assert.True(tall.PixelSize.Width < 1000);
    }

    [AvaloniaFact]
    public void 見つからない画像と壊れた画像は状態だけ返して落ちない()
    {
        using var loader = new BoardBackgroundLoader();
        Assert.Null(Wait(loader.LoadAsync(Path.Combine(_dir, "nothing.png"))));
        Assert.Equal(BackgroundLoadStatus.NotFound, loader.LastStatus);

        string broken = Path.Combine(_dir, "broken.png");
        File.WriteAllText(broken, "not an image");
        Assert.Null(Wait(loader.LoadAsync(broken)));
        Assert.Equal(BackgroundLoadStatus.Failed, loader.LastStatus);

        Assert.Null(Wait(loader.LoadAsync(null)));
        Assert.Equal(BackgroundLoadStatus.None, loader.LastStatus);
    }

    [AvaloniaFact]
    public void 背景画像は面の大きさで一度だけ作り置きし_不透明度や表示や非表示では作り直さない()
    {
        var board = new BoardWindow();
        board.Render(Board.CreateDefault(), new AppearanceSettings(), 0);
        board.Show();
        Dispatcher.UIThread.RunJobs();
        using var loader = new BoardBackgroundLoader();
        var image = Wait(loader.LoadAsync(Png("bake.png", 64, 32)))!;
        var backdrop = board.GetVisualDescendants().OfType<BackdropLayer>().Single();

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Fill, ImageOpacity = 100 }, image);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(backdrop.BakedImage);
        int baked = backdrop.BakeCount;
        double scale = board.RenderScaling;
        Assert.Equal(new PixelSize((int)Math.Ceiling(backdrop.Bounds.Width * scale), (int)Math.Ceiling(backdrop.Bounds.Height * scale)), backdrop.BakedImage!.PixelSize);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Fill, ImageOpacity = 50, Overlay = 0 }, image); // 不透明度・覆いだけ
        board.Hide();
        board.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(baked, backdrop.BakeCount);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Tile, ImageOpacity = 50 }, image); // 表示方法
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(baked + 1, backdrop.BakeCount);

        var data = Board.CreateDefault();
        data.Pages[0].Cols += 2; // 面の大きさ
        board.Render(data, new AppearanceSettings(), 0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(baked + 2, backdrop.BakeCount);
        Assert.False(backdrop.UsesFallback);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 盤面に画像を敷くと覆いが重なり_並べるは原寸のタイル_なしに戻すと消える()
    {
        var board = new BoardWindow();
        board.Render(Board.CreateDefault(), new AppearanceSettings(), 0);
        board.Show();
        using var loader = new BoardBackgroundLoader();
        var image = Wait(loader.LoadAsync(Png("bg.png", 64, 32)))!;
        var backdrop = board.GetVisualDescendants().OfType<BackdropLayer>().Single();
        var overlay = board.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Overlay");

        var chrome = board.GetVisualDescendants().OfType<FrameChrome>().Single();
        var surface = chrome.SurfaceBrush;
        Assert.NotNull(surface);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Fill, Overlay = 40, ImageOpacity = 60 }, image);
        Assert.Same(image, backdrop.Source);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(chrome.SurfaceBrush).Color); // 地の色は描かない
        Assert.Equal(0.6, backdrop.ImageOpacity, 3);
        Assert.Equal(1, backdrop.Opacity); // 不透明度は描くときに掛ける（要素の Opacity は 1 のまま）
        Assert.Equal(BackgroundFit.Fill, backdrop.Fit);
        Assert.True(overlay.IsVisible);
        Assert.Equal(0.4, overlay.Opacity, 3);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Tile, Overlay = 0 }, image);
        Assert.Equal(BackgroundFit.Tile, backdrop.Fit);
        Assert.False(overlay.IsVisible);

        board.ApplyBackground(new BackgroundSettings(), null);
        Assert.Null(backdrop.Source);
        Assert.Null(backdrop.BakedImage);
        Assert.Equal(((ISolidColorBrush)surface!).Color, ((ISolidColorBrush)chrome.SurfaceBrush!).Color); // 画像を外すと地の色に戻る
        Assert.False(overlay.IsVisible);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 設定画面で画像を選ぶとコピーして設定し_クリアで消える()
    {
        var paths = new DataPaths(Path.Combine(_dir, "data"), IsPortable: false);
        var store = new AppDataStore(paths);
        var hub = new SettingsHub(store, store.LoadAll().Settings.Value);
        var w = new SettingsWindow(new SettingsContext(hub, new FakePlatform(), paths, () => null, _ => { }, () => { },
            BackgroundStatus: () => BackgroundLoadStatus.NotFound));
        w.Show();
        Dispatcher.UIThread.RunJobs();
        var tabs = w.GetVisualDescendants().OfType<TabControl>().First();
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == Strings.Settings_Tab_Appearance);
        Dispatcher.UIThread.RunJobs();
        Button Clear() => w.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string) == Strings.Settings_Appearance_Background_Clear);
        Assert.False(Clear().IsEnabled);

        w.ApplyBackgroundFileAsync(Png("pick.png", 40, 20)).Wait();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("background/pick.png", hub.Current.Appearance.Background.Image);
        Assert.True(File.Exists(Path.Combine(paths.BackgroundDirectory, "pick.png")));
        Assert.True(Clear().IsEnabled);
        Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.IsVisible && t.Text == Strings.FormatSettings_Appearance_BackgroundMissing("background/pick.png"));

        Clear().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(hub.Current.Appearance.Background.Image);
        Assert.False(File.Exists(Path.Combine(paths.BackgroundDirectory, "pick.png")));
        w.Close();
    }

    private (SettingsWindow Window, SettingsHub Hub, DataPaths Paths) OpenAppearanceTab()
    {
        var paths = new DataPaths(Path.Combine(_dir, "data"), IsPortable: false);
        var store = new AppDataStore(paths);
        var hub = new SettingsHub(store, store.LoadAll().Settings.Value);
        var w = new SettingsWindow(new SettingsContext(hub, new FakePlatform(), paths, () => null, _ => { }, () => { }));
        w.Show();
        Dispatcher.UIThread.RunJobs();
        var tabs = w.GetVisualDescendants().OfType<TabControl>().First();
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == Strings.Settings_Tab_Appearance);
        Dispatcher.UIThread.RunJobs();
        return (w, hub, paths);
    }

    [AvaloniaFact]
    public void サムネイルへのドロップは最初の対応画像を設定し_対応画像が無ければ何も変えない()
    {
        var (w, hub, paths) = OpenAppearanceTab();
        string txt = Path.Combine(_dir, "note.txt");
        File.WriteAllText(txt, "x");

        w.ApplyDroppedPathsAsync([txt]).Wait();
        w.ApplyDroppedPathsAsync([_dir]).Wait(); // フォルダ
        w.ApplyDroppedPathsAsync([]).Wait();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(hub.Current.Appearance.Background.Image);

        w.ApplyDroppedPathsAsync([txt, Png("drop.png", 40, 20), Png("second.png", 40, 20)]).Wait();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("background/drop.png", hub.Current.Appearance.Background.Image);
        Assert.True(File.Exists(Path.Combine(paths.BackgroundDirectory, "drop.png")));
        w.Close();
    }

    [AvaloniaFact]
    public void ドロップを受けるのはサムネイルだけで_ドラッグ中は枠を強調しヒントを出す()
    {
        var (w, _, _) = OpenAppearanceTab();
        var preview = w.GetVisualDescendants().OfType<Border>().Single(b => b.Width == 120 && b.Height == 68);
        Assert.True(DragDrop.GetAllowDrop(preview));
        Assert.All(w.GetVisualDescendants().OfType<Button>(), b => Assert.False(DragDrop.GetAllowDrop(b)));

        TextBlock Text(string s) => preview.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == s);
        IBrush? Res(string key) => w.TryFindResource(key, w.ActualThemeVariant, out var v) ? v as IBrush : null;

        Assert.True(Text(Strings.Common_None).IsVisible);
        w.SetDropHighlight(true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Res("FlDropBorder"), preview.BorderBrush);
        Assert.True(Text(Strings.Settings_Appearance_Background_DropHint).IsVisible);
        Assert.False(Text(Strings.Common_None).IsVisible);

        w.SetDropHighlight(false);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Res("FlEmptySlotBorder"), preview.BorderBrush);
        Assert.False(Text(Strings.Settings_Appearance_Background_DropHint).IsVisible);
        Assert.True(Text(Strings.Common_None).IsVisible);
        w.Close();
    }
}
