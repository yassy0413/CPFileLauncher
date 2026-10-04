using System.IO.Compression;
using System.Text;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public sealed class DataArchiveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherTests", Guid.NewGuid().ToString("N"));
    private readonly DataPaths _paths;

    public DataArchiveTests()
    {
        _paths = new DataPaths(Path.Combine(_dir, "data"), IsPortable: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private AppDataStore Store()
    {
        var store = new AppDataStore(_paths);
        store.LoadAll();
        return store;
    }

    private static MemoryStream Zip(params (string Name, string Text)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                w.Write(text);
            }
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void エクスポートしたzipを別のデータフォルダへインポートすると設定と盤面が移る()
    {
        var store = Store();
        var settings = new AppSettings();
        settings.Appearance.Opacity = 55;
        store.SaveSettings(settings);
        var board = Board.CreateDefault();
        board.Pages[0].Name = "仕事";
        store.SaveBoard(board);

        using var zip = new MemoryStream();
        DataArchive.Export(_paths, zip);
        zip.Position = 0;

        var otherPaths = new DataPaths(Path.Combine(_dir, "other"), IsPortable: false);
        var other = new AppDataStore(otherPaths);
        var (os, ob) = other.LoadAll();
        other.SaveSettings(os.Value); // 差し替え前のファイルがある状態（起動時に既定値を書く）
        other.SaveBoard(ob.Value);
        other.Import(DataArchive.Read(zip), new DateTime(2026, 10, 3, 12, 0, 0));

        var (s, b) = new AppDataStore(otherPaths).LoadAll();
        Assert.Equal(55, s.Value.Appearance.Opacity);
        Assert.Equal("仕事", b.Value.Pages[0].Name);
        Assert.True(File.Exists(Path.Combine(otherPaths.BackupDirectory, "import-before-settings-20261003-120000.json")));
        Assert.True(File.Exists(Path.Combine(otherPaths.BackupDirectory, "import-before-board-20261003-120000.json")));
    }

    [Fact]
    public void インポートの後はこのプロセスから保存しない()
    {
        var store = Store();
        using var zip = Zip((DataArchive.SettingsEntry, """{ "schemaVersion": 2, "appearance": { "opacity": 40 } }"""));
        store.Import(DataArchive.Read(zip), DateTime.Now);

        var stale = new AppSettings(); // 終了処理が古い設定を書こうとしても
        store.SaveSettings(stale);

        Assert.True(store.SavingSuspended);
        Assert.Equal(40, new AppDataStore(_paths).LoadAll().Settings.Value.Appearance.Opacity);
    }

    [Fact]
    public void 片方だけ入ったzipは入っているほうだけ差し替える()
    {
        var store = Store();
        var board = Board.CreateDefault();
        board.Pages[0].Name = "残る";
        store.SaveBoard(board);

        using var zip = Zip(("FileLauncher-backup/settings.json", """{ "schemaVersion": 2 }""")); // フォルダごと圧縮されていても見つける
        store.Import(DataArchive.Read(zip), DateTime.Now);

        Assert.Equal("残る", new AppDataStore(_paths).LoadAll().Board.Value.Pages[0].Name);
    }

    [Fact]
    public void 関係のないzipや壊れたJSONはインポートできない()
    {
        using (var empty = Zip(("readme.txt", "hello")))
            Assert.Equal(ArchiveError.NoKnownEntries, Assert.Throws<ArchiveException>(() => DataArchive.Read(empty)).Kind);
        using (var broken = Zip((DataArchive.BoardEntry, "{ not json")))
            Assert.Equal(ArchiveError.EntryCorrupt, Assert.Throws<ArchiveException>(() => DataArchive.Read(broken)).Kind);
        using (var array = Zip((DataArchive.SettingsEntry, "[1, 2]")))
            Assert.Equal(ArchiveError.EntryNotObject, Assert.Throws<ArchiveException>(() => DataArchive.Read(array)).Kind);
        using (var notZip = new MemoryStream(Encoding.UTF8.GetBytes("plain text")))
            Assert.Equal(ArchiveError.NotZip, Assert.Throws<ArchiveException>(() => DataArchive.Read(notZip)).Kind);
    }

    [Fact]
    public void ハードウェアアクセラレーションの先読みは_falseのときだけOFFになる()
    {
        Directory.CreateDirectory(_dir);
        string file = Path.Combine(_dir, "settings.json");
        Assert.True(SettingsPeek.HardwareAcceleration(file)); // ファイルなし

        File.WriteAllText(file, """{ "advanced": { "logging": true } }""");
        Assert.True(SettingsPeek.HardwareAcceleration(file)); // キーなし

        File.WriteAllText(file, """{ "advanced": { "hardwareAcceleration": false } }""");
        Assert.False(SettingsPeek.HardwareAcceleration(file));

        File.WriteAllText(file, """{ "advanced": { "hardwareAcceleration": fal""");
        Assert.True(SettingsPeek.HardwareAcceleration(file)); // 壊れた JSON
    }
}
