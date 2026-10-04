using System.Text.Json.Nodes;
using FileLauncher.Core.Model;

namespace FileLauncher.Core.Storage;

/// <summary>settings.json と board.json をまとめて扱う入口。</summary>
public sealed class AppDataStore
{
    public DataPaths Paths { get; }
    public VersionedJsonStore<AppSettings> SettingsStore { get; }
    public VersionedJsonStore<Board> BoardStore { get; }

    /// <summary>board.json のバックアップ世代数は設定値に従うため、読み込んだ設定を参照する。</summary>
    private AppSettings? _settings;

    public AppDataStore(DataPaths paths, TimeProvider? time = null)
    {
        Paths = paths;
        SettingsStore = new VersionedJsonStore<AppSettings>(
            paths.SettingsFile,
            AppSettings.CurrentSchemaVersion,
            createDefault: () => new AppSettings(),
            migrateOneStep: MigrateSettings,
            normalize: s => s.Normalize(),
            time: time);

        BoardStore = new VersionedJsonStore<Board>(
            paths.BoardFile,
            Board.CurrentSchemaVersion,
            createDefault: () => Board.CreateDefault(
                _settings?.Appearance.DefaultRows ?? Page.DefaultRows,
                _settings?.Appearance.DefaultCols ?? Page.DefaultCols),
            backupDirectory: paths.BackupDirectory,
            backupGenerations: () => _settings?.Data.BackupGenerations ?? 10,
            migrateOneStep: MigrateBoard,
            normalize: b => b.Normalize(),
            time: time,
            backupMinInterval: BoardBackupInterval);
    }

    /// <summary>盤面の世代バックアップの最短間隔（SPEC §9.3。編集のたびに保存されるため）。</summary>
    public static readonly TimeSpan BoardBackupInterval = TimeSpan.FromMinutes(10);

    /// <summary>設定 → 盤面の順に読む（盤面の既定値・バックアップ世代数が設定に依存するため）。</summary>
    public (LoadResult<AppSettings> Settings, LoadResult<Board> Board) LoadAll()
    {
        Paths.EnsureCreated();
        var settings = SettingsStore.Load();
        _settings = settings.Value;
        var board = BoardStore.Load();
        return (settings, board);
    }

    public void SaveSettings(AppSettings settings)
    {
        if (SavingSuspended) return;
        _settings = settings;
        SettingsStore.Save(settings);
    }

    public void SaveBoard(Board board)
    {
        if (SavingSuspended) return;
        BoardStore.Save(board);
    }

    /// <summary>インポートで差し替えた後は、再起動までこのプロセスから保存しない（終了処理の保存で差し替えを上書きしないため）。</summary>
    public bool SavingSuspended { get; private set; }

    /// <summary>
    /// エクスポートした zip の中身で settings.json / board.json を差し替える（入っているほうだけ）。
    /// 差し替え前のファイルは backup/ に import-before-*.json として残す（盤面の世代バックアップとは別名）。
    /// 呼んだ後は保存を止めるので、呼び出し側はすぐ再起動すること。
    /// </summary>
    public void Import(ArchiveContents contents, DateTime localNow)
    {
        Paths.EnsureCreated();
        Directory.CreateDirectory(Paths.BackupDirectory);
        string stamp = localNow.ToString("yyyyMMdd-HHmmss");
        foreach (var (text, file, name) in new[] { (contents.Settings, Paths.SettingsFile, "settings"), (contents.Board, Paths.BoardFile, "board") })
        {
            if (text is null) continue;
            if (File.Exists(file)) File.Copy(file, Path.Combine(Paths.BackupDirectory, $"import-before-{name}-{stamp}.json"), overwrite: true);
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, file, overwrite: true);
        }
        if (contents.Files is { Count: > 0 } files)
        {
            // 背景画像: background/ は 1 枚だけなので、入れ替える前に今の中身を消す
            if (Directory.Exists(Paths.BackgroundDirectory))
                foreach (var old in Directory.GetFiles(Paths.BackgroundDirectory)) File.Delete(old);
            Directory.CreateDirectory(Paths.BackgroundDirectory);
            foreach (var (key, bytes) in files)
                File.WriteAllBytes(Path.Combine(Paths.BackgroundDirectory, Path.GetFileName(key)), bytes);
        }
        SavingSuspended = true;
    }

    // schemaVersion を上げるときはここに 1 段ずつ追加する
    private static JsonObject MigrateSettings(JsonObject data, int fromVersion)
    {
        switch (fromVersion)
        {
            case 1:
                // v1 → v2: popup.position / x / y（1 組）を keyboard / mouse の 2 組へ複写（SPEC §9.3）
                if (data["popup"] is JsonObject popup && (popup.ContainsKey("position") || popup.ContainsKey("x") || popup.ContainsKey("y")))
                {
                    JsonNode? Take(string key)
                    {
                        var n = popup[key];
                        popup.Remove(key);
                        return n;
                    }
                    var position = Take("position");
                    var x = Take("x");
                    var y = Take("y");
                    JsonObject Group() => new() // JsonNode は親を 1 つしか持てないので複製する
                    {
                        ["position"] = position?.DeepClone(),
                        ["x"] = x?.DeepClone(),
                        ["y"] = y?.DeepClone(),
                    };
                    popup["keyboard"] = Group();
                    popup["mouse"] = Group();
                }
                return data;
            case 2:
                // v2 → v3: テーマの選択を廃止（サイバーパンク専用）し、基調色プリセット accent を主色・副色 colors に（SPEC §9.3、2026-10-04）
                if (data["appearance"] is JsonObject appearance)
                {
                    appearance.Remove("theme");
                    string? accent = appearance["accent"] is JsonValue v && v.TryGetValue<string>(out var a) ? a : null;
                    appearance.Remove("accent");
                    var preset = Enum.TryParse<AccentPreset>(accent, ignoreCase: true, out var id) ? id : AccentPreset.Cyan;
                    var (primary, secondary) = Theming.ColorPresets.For(preset);
                    if (!appearance.ContainsKey("colors"))
                        appearance["colors"] = new JsonObject { ["primary"] = primary.ToHex(), ["secondary"] = secondary.ToHex() };
                }
                return data;
            default:
                throw new InvalidDataException($"settings.json: no migration from schemaVersion {fromVersion}");
        }
    }

    private static JsonObject MigrateBoard(JsonObject data, int fromVersion) =>
        throw new InvalidDataException($"board.json: no migration from schemaVersion {fromVersion}");
}
