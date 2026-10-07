using System.Text.Json;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public sealed class BackgroundImageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherTests", Guid.NewGuid().ToString("N"));
    private readonly DataPaths _paths;

    public BackgroundImageTests()
    {
        _paths = new DataPaths(Path.Combine(_dir, "data"), IsPortable: false);
        Directory.CreateDirectory(_paths.Root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Source(string name, string content = "img")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("A.JPG", true)]
    [InlineData("x/y.webp", true)]
    [InlineData("a.svg", false)]
    [InlineData("a.txt", false)]
    [InlineData("noext", false)]
    [InlineData("pics/", false)]
    public void 背景画像にできるかは拡張子だけで大文字小文字を区別せずに決める(string path, bool expected)
        => Assert.Equal(expected, BackgroundImageStore.IsSupported(path));

    [Fact]
    public void ドロップされたパスからは最初の対応ファイルを選び_無ければnull()
    {
        Assert.Equal("b.png", BackgroundImageStore.FirstSupported(["a.txt", "b.png", "c.jpg"]));
        Assert.Null(BackgroundImageStore.FirstSupported(["a.txt", "dir/"]));
        Assert.Null(BackgroundImageStore.FirstSupported([]));
    }

    [Fact]
    public void 選んだ画像はbackgroundへコピーされ_次の画像を選ぶと前のコピーは消える()
    {
        string a = BackgroundImageStore.Import(_paths, Source("a.png"));
        Assert.Equal("background/a.png", a);
        Assert.True(File.Exists(Path.Combine(_paths.BackgroundDirectory, "a.png")));
        Assert.Equal(Path.Combine(_paths.BackgroundDirectory, "a.png"), BackgroundImageStore.Resolve(_paths, a));

        string b = BackgroundImageStore.Import(_paths, Source("b.jpg"));
        Assert.Equal("background/b.jpg", b);
        Assert.Equal(["b.jpg"], Directory.GetFiles(_paths.BackgroundDirectory).Select(Path.GetFileName));

        // すでに background/ の中にあるものを選び直しても消えない
        Assert.Equal(b, BackgroundImageStore.Import(_paths, BackgroundImageStore.Resolve(_paths, b)!));
        Assert.True(File.Exists(BackgroundImageStore.Resolve(_paths, b)));
    }

    [Fact]
    public void クリアで消すのはコピーだけで_絶対パスの元ファイルは消さない()
    {
        string original = Source("photo.png");
        BackgroundImageStore.Remove(_paths, original);
        Assert.True(File.Exists(original));
        Assert.Equal(original, BackgroundImageStore.Resolve(_paths, original));

        string copy = BackgroundImageStore.Import(_paths, original);
        BackgroundImageStore.Remove(_paths, copy);
        Assert.False(File.Exists(BackgroundImageStore.Resolve(_paths, copy)));
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void 覆いは0から90の5刻みに丸められ_既定は埋める40パーセント()
    {
        var s = new AppSettings();
        Assert.Null(s.Appearance.Background.Image);
        Assert.Equal((BackgroundFit.Fill, 40), (s.Appearance.Background.Fit, s.Appearance.Background.Overlay));

        s.Appearance.Background.Overlay = 97;
        s.Appearance.Background.Image = "  ";
        s.Normalize();
        Assert.Equal(90, s.Appearance.Background.Overlay);
        Assert.Null(s.Appearance.Background.Image);
        s.Appearance.Background.Overlay = 23;
        s.Normalize();
        Assert.Equal(25, s.Appearance.Background.Overlay);

        Assert.Equal(100, new AppSettings().Appearance.Background.ImageOpacity);
        s.Appearance.Background.ImageOpacity = 3;
        s.Normalize();
        Assert.Equal(10, s.Appearance.Background.ImageOpacity);
        s.Appearance.Background.ImageOpacity = 62;
        s.Normalize();
        Assert.Equal(60, s.Appearance.Background.ImageOpacity);
    }

    [Fact]
    public void 背景の設定はJSONで往復し_キーが無い古い設定も読める()
    {
        var s = new AppSettings();
        s.Appearance.Background = new BackgroundSettings { Image = "background/a.png", Fit = BackgroundFit.Tile, Overlay = 55 };
        string json = JsonSerializer.Serialize(s, JsonDefaults.Options);
        Assert.Contains("\"fit\": \"tile\"", json);
        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)!;
        Assert.Equal(("background/a.png", BackgroundFit.Tile, 55), (back.Appearance.Background.Image, back.Appearance.Background.Fit, back.Appearance.Background.Overlay));

        var old = JsonSerializer.Deserialize<AppSettings>("""{ "schemaVersion": 2, "appearance": { "opacity": 70 } }""", JsonDefaults.Options)!;
        old.Normalize();
        Assert.Null(old.Appearance.Background.Image);
    }

    [Fact]
    public void エクスポートにはコピーした背景画像も入り_インポートで戻る()
    {
        var store = new AppDataStore(_paths);
        var (settings, board) = store.LoadAll();
        settings.Value.Appearance.Background.Image = BackgroundImageStore.Import(_paths, Source("wall.png", "pixels"));
        store.SaveSettings(settings.Value);
        store.SaveBoard(board.Value);

        using var zip = new MemoryStream();
        DataArchive.Export(_paths, zip);
        zip.Position = 0;
        var contents = DataArchive.Read(zip);
        Assert.Equal("pixels", System.Text.Encoding.UTF8.GetString(contents.Files!["background/wall.png"]));

        var otherPaths = new DataPaths(Path.Combine(_dir, "other"), IsPortable: false);
        var other = new AppDataStore(otherPaths);
        other.LoadAll();
        Directory.CreateDirectory(otherPaths.BackgroundDirectory);
        File.WriteAllText(Path.Combine(otherPaths.BackgroundDirectory, "old.png"), "x");
        other.Import(contents, DateTime.Now);

        Assert.Equal(["wall.png"], Directory.GetFiles(otherPaths.BackgroundDirectory).Select(Path.GetFileName));
        var loaded = new AppDataStore(otherPaths).LoadAll().Settings.Value;
        Assert.True(File.Exists(BackgroundImageStore.Resolve(otherPaths, loaded.Appearance.Background.Image)));
    }
}
