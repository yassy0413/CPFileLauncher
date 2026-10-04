using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
    public void 盤面に画像を敷くと覆いが重なり_並べるは原寸のタイル_なしに戻すと消える()
    {
        var board = new BoardWindow();
        board.Render(Board.CreateDefault(), new AppearanceSettings(), 0);
        board.Show();
        using var loader = new BoardBackgroundLoader();
        var image = Wait(loader.LoadAsync(Png("bg.png", 64, 32)))!;
        var backdrop = board.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Backdrop");
        var overlay = board.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Overlay");

        var chrome = board.GetVisualDescendants().OfType<FrameChrome>().Single();
        var surface = chrome.SurfaceBrush;
        Assert.NotNull(surface);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Fill, Overlay = 40, ImageOpacity = 60 }, image);
        var brush = Assert.IsType<ImageBrush>(backdrop.Background);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(chrome.SurfaceBrush).Color); // 地の色は描かない
        Assert.Equal(0.6, backdrop.Opacity, 3);
        Assert.Equal(Stretch.UniformToFill, brush.Stretch);
        Assert.True(overlay.IsVisible);
        Assert.Equal(0.4, overlay.Opacity, 3);

        board.ApplyBackground(new BackgroundSettings { Image = "x", Fit = BackgroundFit.Tile, Overlay = 0 }, image);
        brush = Assert.IsType<ImageBrush>(backdrop.Background);
        Assert.Equal(TileMode.Tile, brush.TileMode);
        Assert.Equal(new RelativeRect(0, 0, 64, 32, RelativeUnit.Absolute), brush.DestinationRect);
        Assert.False(overlay.IsVisible);

        board.ApplyBackground(new BackgroundSettings(), null);
        Assert.Null(backdrop.Background);
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
}
