using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public class ItemFactoryTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly Dictionary<string, ShortcutInfo> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly ItemFactory _factory;

    public ItemFactoryTests() => _factory = new ItemFactory(p => _links.GetValueOrDefault(p));

    public void Dispose() => _dir.Dispose();

    private string Touch(string name)
    {
        var path = _dir.File(name);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void ファイルは拡張子なしの名前で登録_実行ファイルはアプリ扱い()
    {
        var doc = _factory.FromPath(Touch("見積書.xlsx"))!;
        var exe = _factory.FromPath(Touch("tool.exe"))!;
        var bat = _factory.FromPath(Touch("build.bat"))!;

        Assert.Equal((ItemKind.File, "見積書"), (doc.Kind, doc.Name));
        Assert.Equal((ItemKind.App, "tool"), (exe.Kind, exe.Name));
        Assert.Equal(ItemKind.App, bat.Kind);
    }

    [Fact]
    public void フォルダはフォルダ名で登録()
    {
        var sub = Directory.CreateDirectory(_dir.File("案件A")).FullName;

        var item = _factory.FromPath(sub + Path.DirectorySeparatorChar)!;

        Assert.Equal((ItemKind.Folder, "案件A"), (item.Kind, item.Name));
    }

    [Fact]
    public void 末尾区切り付きで届いたフォルダは区切りを除いて保存する()
    {
        var sub = Directory.CreateDirectory(_dir.File("資料")).FullName;

        var item = _factory.FromPath(sub + Path.DirectorySeparatorChar)!;

        Assert.Equal(sub, item.Target);
    }

    [Fact]
    public void Macのappバンドルは末尾区切り付きで届いてもアプリ扱い_名前は拡張子なし()
    {
        var app = Directory.CreateDirectory(_dir.File("Safari.app")).FullName;

        var item = _factory.FromPath(app + "/")!;

        Assert.Equal((ItemKind.App, "Safari", app), (item.Kind, item.Name, item.Target));
    }

    [Fact]
    public void 拡張子の無いFinderエイリアスも解決し_appを指していればアプリ扱い()
    {
        var app = Directory.CreateDirectory(_dir.File("Notes.app")).FullName;
        var alias = Touch("メモ のエイリアス");
        _links[alias] = new ShortcutInfo(app + "/", "", "");

        var item = _factory.FromPath(alias)!;

        Assert.Equal((ItemKind.App, app, alias, "メモ のエイリアス"), (item.Kind, item.Target, item.LinkPath, item.Name));
    }

    [Fact]
    public void 存在しないパスは登録しない()
    {
        Assert.Null(_factory.FromPath(_dir.File("nope.txt")));
        Assert.Null(_factory.FromPath("   "));
    }

    [Fact]
    public void lnkはリンク先を解決し_元のlnkと引数と作業フォルダを保持する()
    {
        var target = Touch("app.exe");
        var lnk = Touch("アプリ.lnk");
        _links[lnk] = new ShortcutInfo(target, "--flag", _dir.Path);

        var item = _factory.FromPath(lnk)!;

        Assert.Equal(ItemKind.App, item.Kind);
        Assert.Equal(target, item.Target);
        Assert.Equal(lnk, item.LinkPath);
        Assert.Equal("アプリ", item.Name);
        Assert.Equal("--flag", item.Args);
        Assert.Equal(_dir.Path, item.WorkingDir);
    }

    [Fact]
    public void リンク先がフォルダのlnkはフォルダ扱い()
    {
        var folder = Directory.CreateDirectory(_dir.File("docs")).FullName;
        var lnk = Touch("docs.lnk");
        _links[lnk] = new ShortcutInfo(folder, "", "");

        Assert.Equal(ItemKind.Folder, _factory.FromPath(lnk)!.Kind);
    }

    [Fact]
    public void シェル項目へのlnkはlnk自体を起動対象にする()
    {
        var lnk = Touch("エクスプローラー.lnk");
        _links[lnk] = new ShortcutInfo("", "", "");

        var item = _factory.FromPath(lnk)!;

        Assert.Equal((ItemKind.App, lnk, lnk), (item.Kind, item.Target, item.LinkPath));
    }

    [Fact]
    public void URLテキストはURLアイテム_名前はホスト名()
    {
        var item = _factory.FromText("https://teamspirit-9745.lightning.force.com/lightning/n/teamspirit__AtkEmpJobTab\r\n")!;

        Assert.Equal(ItemKind.Url, item.Kind);
        Assert.Equal("teamspirit-9745.lightning.force.com", item.Name);
        Assert.StartsWith("https://", item.Target);
    }

    [Fact]
    public void パス文字列やfileURLのテキストはパスとして登録()
    {
        var file = Touch("memo.txt");

        Assert.Equal(file, _factory.FromText($"\"{file}\"")!.Target);
        Assert.Equal(file, _factory.FromText(new Uri(file).AbsoluteUri)!.Target);
        Assert.Null(_factory.FromText("ただの文章"));
    }
}

public class BoardEditingTests
{
    private static Page PageWith(int rows, int cols, params (int R, int C)[] occupied)
    {
        var page = new Page { Rows = rows, Cols = cols };
        foreach (var (r, c) in occupied) page.Items.Add(new LauncherItem { Row = r, Col = c, Name = $"{r},{c}" });
        return page;
    }

    private static List<LauncherItem> NewItems(int n) => Enumerable.Range(0, n).Select(i => new LauncherItem { Name = $"n{i}" }).ToList();

    [Fact]
    public void ドロップ位置から右へ_行末で次の行へ_埋まっている所は飛ばす()
    {
        var page = PageWith(3, 4, (0, 2), (1, 0));
        var items = NewItems(3);

        int placed = BoardEditing.Place(page, 0, 1, items);

        Assert.Equal(3, placed);
        Assert.Equal(new[] { (0, 1), (0, 3), (1, 1) }, items.Select(i => (i.Row, i.Col)));
    }

    [Fact]
    public void ドロップ位置より前の空きは使わず_足りない分は置かない()
    {
        var page = PageWith(2, 2);
        var items = NewItems(3);

        int placed = BoardEditing.Place(page, 1, 0, items);

        Assert.Equal(2, placed);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public void 足りない分に必要な行数()
    {
        var page = PageWith(2, 4, (1, 3));

        Assert.Equal(0, BoardEditing.ExtraRowsNeeded(page, 1, 0, 3));
        Assert.Equal(1, BoardEditing.ExtraRowsNeeded(page, 1, 0, 4));
        Assert.Equal(2, BoardEditing.ExtraRowsNeeded(page, 1, 0, 8));
    }
}

public class LaunchArgsTests
{
    [Fact]
    public void ドロップしたパスは引用符付きで末尾に足す()
    {
        Assert.Equal("-n \"C:\\a b.txt\" \"C:\\c.txt\"", LaunchArgs.Build("-n", [@"C:\a b.txt", @"C:\c.txt"]));
        Assert.Equal("\"C:\\a.txt\"", LaunchArgs.Build("", [@"C:\a.txt"]));
    }

    [Fact]
    public void パーセント1があればそこへ入れ_引用符を二重にしない()
    {
        Assert.Equal("/open \"C:\\a.txt\" /x", LaunchArgs.Build("/open %1 /x", [@"C:\a.txt"]));
        Assert.Equal("/open \"C:\\a.txt\"", LaunchArgs.Build("/open \"%1\"", [@"C:\a.txt"]));
        Assert.Equal("\"/tmp/a\"", LaunchArgs.Build("$1", ["/tmp/a"]));
    }

    [Fact]
    public void ドロップが無ければ引数そのまま_パーセント1は消す()
    {
        Assert.Equal("-n", LaunchArgs.Build("-n", null));
        Assert.Equal("/open", LaunchArgs.Build("/open %1", []));
    }

    [Fact]
    public void 環境変数とチルダを展開する()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.Combine(home, "x") .Replace('\\', '/'), LaunchArgs.ExpandPath("~/x").Replace('\\', '/'));
        Assert.Equal(home + "/y", LaunchArgs.ExpandPath("$HOME/y"));
        if (OperatingSystem.IsWindows())
            Assert.Equal(home + @"\z", LaunchArgs.ExpandPath(@"%USERPROFILE%\z"));
    }
}

public class IconCacheKeyTests
{
    [Fact]
    public void 上書きアイコン_lnk_対象_URLの順で取得元を決める()
    {
        Assert.Equal(@"C:\i.png", IconCacheKey.SourceOf(new LauncherItem { Target = @"C:\a.exe", LinkPath = @"C:\a.lnk", IconOverride = @"C:\i.png" }));
        Assert.Equal(@"C:\a.lnk", IconCacheKey.SourceOf(new LauncherItem { Target = @"C:\a.exe", LinkPath = @"C:\a.lnk" }));
        Assert.Equal(@"C:\a.exe", IconCacheKey.SourceOf(new LauncherItem { Target = @"C:\a.exe" }));
        // URL は既定ブラウザのアイコンなのでスキームごとに共有
        Assert.Equal("url:https", IconCacheKey.SourceOf(new LauncherItem { Kind = ItemKind.Url, Target = "https://a.example/x" }));
        Assert.Equal("url:https", IconCacheKey.SourceOf(new LauncherItem { Kind = ItemKind.Url, Target = "https://b.example/" }));
    }

    [Fact]
    public void サイズ_方式_ファイル更新で別のキーになる()
    {
        using var dir = new TempDir();
        var file = dir.File("a.txt");
        File.WriteAllText(file, "1");
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var k = IconCacheKey.FileName(file, 48, "t0");
        Assert.Equal(k, IconCacheKey.FileName(file.ToUpperInvariant(), 48, "t0")); // 大文字小文字は区別しない
        Assert.NotEqual(k, IconCacheKey.FileName(file, 72, "t0"));
        Assert.NotEqual(k, IconCacheKey.FileName(file, 48, "t1"));

        File.SetLastWriteTimeUtc(file, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.NotEqual(k, IconCacheKey.FileName(file, 48, "t0"));
        Assert.EndsWith(".png", k);
    }
}

public class FileTransferTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Write(string relative, string content)
    {
        var path = _dir.File(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string Dest => _dir.File("dest");

    private string In(string name) => Path.Combine(Dest, name);

    [Fact]
    public void 同名が無ければそのままの名前でコピーする()
    {
        Directory.CreateDirectory(Dest);
        var src = Write("src/報告書.pdf", "a");

        var r = FileTransfer.Transfer(Dest, [src], move: false);

        Assert.Equal(new TransferResult(1, 0, 0, 0), r);
        Assert.Equal("a", File.ReadAllText(In("報告書.pdf")));
        Assert.True(File.Exists(src));
    }

    [Fact]
    public void 同名で内容も同じならスキップし_移動でも元を消さない()
    {
        var src = Write("src/報告書.pdf", "same");
        Write("dest/報告書.pdf", "same");

        var r = FileTransfer.Transfer(Dest, [src], move: true);

        Assert.Equal(new TransferResult(0, 0, 1, 0), r);
        Assert.True(File.Exists(src));
        Assert.False(File.Exists(In("報告書 2.pdf")));
    }

    [Fact]
    public void 同名で内容が違えば名前2にする()
    {
        var src = Write("src/a.txt", "new");
        Write("dest/a.txt", "old");

        var r = FileTransfer.Transfer(Dest, [src], move: false);

        Assert.Equal(new TransferResult(0, 1, 0, 0), r);
        Assert.Equal("new", File.ReadAllText(In("a 2.txt")));
        Assert.Equal("old", File.ReadAllText(In("a.txt")));
    }

    [Fact]
    public void 名前2も別内容なら名前3_名前2が同一ならスキップ()
    {
        var src = Write("src/a.txt", "new");
        Write("dest/a.txt", "old");
        Write("dest/a 2.txt", "older");
        var src2 = Write("src2/b.txt", "same");
        Write("dest/b.txt", "other");
        Write("dest/b 2.txt", "same");

        var r = FileTransfer.Transfer(Dest, [src, src2], move: false);

        Assert.Equal(new TransferResult(0, 1, 1, 0), r);
        Assert.Equal("new", File.ReadAllText(In("a 3.txt")));
        Assert.False(File.Exists(In("b 3.txt")));
    }

    [Fact]
    public void フォルダは中身が同じならスキップ_違えば拡張子を分けずに連番を付ける()
    {
        Write("src/v1.2/a.txt", "a");
        Write("dest/v1.2/a.txt", "a");
        Write("src2/v1.2/a.txt", "a");
        Write("src2/v1.2/sub/b.txt", "b");

        var same = FileTransfer.Transfer(Dest, [_dir.File("src/v1.2") + "/"], move: false);
        var diff = FileTransfer.Transfer(Dest, [_dir.File("src2/v1.2")], move: false);

        Assert.Equal(new TransferResult(0, 0, 1, 0), same);
        Assert.Equal(new TransferResult(0, 1, 0, 0), diff);
        Assert.Equal("b", File.ReadAllText(Path.Combine(In("v1.2 2"), "sub", "b.txt")));
    }

    [Fact]
    public void 移動すると元が消える()
    {
        Directory.CreateDirectory(Dest);
        var file = Write("src/memo.txt", "1");
        Write("src/folder/x.txt", "x");

        var r = FileTransfer.Transfer(Dest, [file, _dir.File("src/folder")], move: true);

        Assert.Equal(new TransferResult(2, 0, 0, 0), r);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(_dir.File("src/folder")));
        Assert.True(File.Exists(Path.Combine(In("folder"), "x.txt")));
    }

    [Fact]
    public void 同じドロップ内で同名が重なっても別々の名前にする()
    {
        Directory.CreateDirectory(Dest);
        var a = Write("x/memo.txt", "1");
        var b = Write("y/memo.txt", "2");

        var r = FileTransfer.Transfer(Dest, [a, b], move: false);

        Assert.Equal(new TransferResult(1, 1, 0, 0), r);
        Assert.Equal("2", File.ReadAllText(In("memo 2.txt")));
    }

    [Fact]
    public void 候補名の列は拡張子の前に番号_フォルダと拡張子なしは末尾に番号()
    {
        Assert.Equal([In("a.tar.gz"), In("a.tar 2.gz"), In("a.tar 3.gz")], FileTransfer.CandidateNames(Dest, "a.tar.gz", false).Take(3));
        Assert.Equal([In("Makefile"), In("Makefile 2")], FileTransfer.CandidateNames(Dest, "Makefile", false).Take(2));
        Assert.Equal([In("v1.2"), In("v1.2 2")], FileTransfer.CandidateNames(Dest, "v1.2", true).Take(2));
        Assert.Equal([In(".gitignore"), In(".gitignore 2")], FileTransfer.CandidateNames(Dest, ".gitignore", false).Take(2));
    }
}
