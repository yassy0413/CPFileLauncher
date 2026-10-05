using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public class DataPathsTests
{
    [Fact]
    public void portable_flagがあれば実行ファイル隣のdataを使う()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("portable.flag"), "");

        var paths = DataPaths.Resolve(dir.Path);

        Assert.True(paths.IsPortable);
        Assert.Equal(dir.File("data"), paths.Root);
        Assert.Equal(Path.Combine(dir.Path, "data", "board.json"), paths.BoardFile);
    }

    [Fact]
    public void portable_flagが無ければユーザーデータ領域のCPFileLauncherフォルダ()
    {
        using var dir = new TempDir();
        using var userData = new TempDir(); // 本物のユーザーデータ領域には触れない（2026-10-04 にテストが本物の旧フォルダを改名した）

        var paths = DataPaths.Resolve(dir.Path, userData.Path);

        Assert.False(paths.IsPortable);
        Assert.Equal("CPFileLauncher", Path.GetFileName(paths.Root));
        Assert.Equal(userData.Path, Path.GetDirectoryName(paths.Root));
    }

    [Fact]
    public void portableflagは実行ファイルCPFileLauncherの隣を探し_dotnetで動かしたときはアプリのディレクトリを探す()
    {
        // Windows では GetDirectoryName が "/" を "\" に直すので、区切りは実行中の OS のものを使う
        string app = Path.Combine(Path.DirectorySeparatorChar + "Applications", "CPFileLauncher.app", "Contents", "MacOS");
        Assert.Equal(app, DataPaths.ExecutableDirectory(Path.Combine(app, "CPFileLauncher"), "/other"));
        Assert.Equal(@"C:\Tools", DataPaths.ExecutableDirectory(@"C:\Tools\CPFileLauncher.exe".Replace('\\', Path.DirectorySeparatorChar), @"C:\Tools").Replace(Path.DirectorySeparatorChar, '\\'));
        Assert.Equal("/repo/bin", DataPaths.ExecutableDirectory("/usr/local/share/dotnet/dotnet", "/repo/bin"));
        Assert.Equal("/repo/bin", DataPaths.ExecutableDirectory(null, "/repo/bin"));
    }

    [Fact]
    public void 改名前のフォルダがあれば新しい名前に移し_新しいフォルダが既にあれば何もしない()
    {
        using var root = new TempDir();
        string legacy = Path.Combine(root.Path, "FileLauncher");
        Directory.CreateDirectory(Path.Combine(legacy, "backup"));
        File.WriteAllText(Path.Combine(legacy, "board.json"), "{}");
        File.WriteAllText(Path.Combine(legacy, "backup", "board-1.json"), "{}");

        Assert.Equal(LegacyMigration.Moved, DataPaths.MigrateLegacyRoot(root.Path));
        Assert.False(Directory.Exists(legacy));
        Assert.True(File.Exists(Path.Combine(root.Path, "CPFileLauncher", "backup", "board-1.json")));

        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{}");
        Assert.Equal(LegacyMigration.None, DataPaths.MigrateLegacyRoot(root.Path)); // 新しいほうが優先
        Assert.True(Directory.Exists(legacy));
    }

    [Fact]
    public void 改名前のフォルダに設定も盤面も無ければ引き継がない()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(Path.Combine(root.Path, "FileLauncher", "logs"));
        Assert.Equal(LegacyMigration.None, DataPaths.MigrateLegacyRoot(root.Path));
        Assert.False(Directory.Exists(Path.Combine(root.Path, "CPFileLauncher")));
    }
}
