namespace FileLauncher.Core.Storage;

/// <summary>データフォルダ内の各ファイルの場所（SPEC §9.1, §9.2）。</summary>
public sealed record DataPaths(string Root, bool IsPortable)
{
    public const string AppFolderName = "CPFileLauncher";

    /// <summary>実行ファイル名（拡張子なし）。portable.flag の探索で自分の実行ファイルかを見分ける。</summary>
    public const string ExecutableName = "CPFileLauncher";

    /// <summary>改名前（2026-10-04 まで）のデータフォルダ名。</summary>
    public const string LegacyFolderName = "FileLauncher";
    public const string PortableFlagFile = "portable.flag";

    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string BoardFile => Path.Combine(Root, "board.json");
    public string IconsDirectory => Path.Combine(Root, "icons");
    public string BackupDirectory => Path.Combine(Root, "backup");
    public string LogsDirectory => Path.Combine(Root, "logs");
    /// <summary>設定画面で選んだ背景画像のコピー（SPEC §3.7、§9.2）。使うときに作る。</summary>
    public string BackgroundDirectory => Path.Combine(Root, BackgroundImageStore.Folder);

    /// <summary>
    /// portable.flag を探すディレクトリ（SPEC §9.1）: 実行ファイルが CPFileLauncher(.exe) ならそのディレクトリ
    /// （.app では Contents/MacOS、Windows の単一ファイルでは exe の隣）、それ以外（dotnet FileLauncher.dll など）は baseDirectory。
    /// </summary>
    public static string ExecutableDirectory(string? processPath, string baseDirectory)
    {
        string? name = processPath is null ? null : Path.GetFileNameWithoutExtension(processPath);
        return name == ExecutableName && Path.GetDirectoryName(processPath) is { Length: > 0 } dir ? dir : baseDirectory;
    }

    /// <summary>直近の Resolve で行った旧フォルダの引き継ぎ（App が起動ログに出す）。</summary>
    public static LegacyMigration LastMigration { get; private set; }

    /// <summary>
    /// 実行ファイルと同じフォルダに portable.flag があればその隣の data/、
    /// なければ OS のユーザーデータ領域（Win: %APPDATA%、Mac: ~/Library/Application Support）。
    /// 非ポータブルでは、改名前のフォルダ（FileLauncher）があれば新しい名前へ引き継ぐ（SPEC §9.1）。
    /// </summary>
    /// <param name="userDataRoot">テスト用: ユーザーデータ領域の代わり（本物のデータフォルダに触れないように）。</param>
    public static DataPaths Resolve(string executableDirectory, string? userDataRoot = null)
    {
        if (File.Exists(Path.Combine(executableDirectory, PortableFlagFile)))
            return new DataPaths(Path.Combine(executableDirectory, "data"), IsPortable: true);

        string root = userDataRoot ?? UserDataRoot();
        var migration = MigrateLegacyRoot(root);
        if (migration != LegacyMigration.None) LastMigration = migration; // Program と App の 2 回呼ばれる。最初の結果を残す
        return new DataPaths(Path.Combine(root, AppFolderName), IsPortable: false);
    }

    /// <summary>
    /// 新しいフォルダが無く、旧フォルダに settings.json か board.json があれば、旧フォルダを新しい名前に変える
    /// （同じボリュームなので一瞬）。変えられなければ（別のプロセスが掴んでいる等）中身をコピーする。
    /// </summary>
    public static LegacyMigration MigrateLegacyRoot(string userDataRoot)
    {
        string legacy = Path.Combine(userDataRoot, LegacyFolderName);
        string current = Path.Combine(userDataRoot, AppFolderName);
        if (Directory.Exists(current) || !Directory.Exists(legacy)) return LegacyMigration.None;
        if (!File.Exists(Path.Combine(legacy, "settings.json")) && !File.Exists(Path.Combine(legacy, "board.json"))) return LegacyMigration.None;
        try
        {
            Directory.Move(legacy, current);
            return LegacyMigration.Moved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                CopyDirectory(legacy, current);
                return LegacyMigration.Copied;
            }
            catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException)
            {
                return LegacyMigration.Failed;
            }
        }
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: false);
        foreach (var dir in Directory.GetDirectories(from)) CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    private static string UserDataRoot()
    {
        // .NET の SpecialFolder.ApplicationData は macOS だと ~/.config になるので明示する
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }
}

/// <summary>改名前のデータフォルダの引き継ぎの結果。</summary>
public enum LegacyMigration
{
    None,
    Moved,
    Copied,
    Failed,
}
