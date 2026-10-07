using System.Text.Json.Nodes;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public class AppDataStoreTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _dir.Dispose();

    private AppDataStore NewStore() => new(new DataPaths(_dir.Path, IsPortable: false), _time);

    private static LauncherItem SampleItem() => new()
    {
        Row = 1,
        Col = 2,
        Kind = ItemKind.App,
        Target = @"%WINDIR%\System32\notepad.exe",
        Name = "メモ帳",
        Args = "%1",
        LinkPath = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\メモ帳.lnk",
        Hotkey = "N",
    };

    [Fact]
    public void ファイルが無ければ既定値を返し_まだ書き込まない()
    {
        var (settings, board) = NewStore().LoadAll();

        Assert.Equal(LoadStatus.CreatedDefault, settings.Status);
        Assert.Equal(LoadStatus.CreatedDefault, board.Status);
        Assert.Single(board.Value.Pages);
        Assert.Equal("Page 1", board.Value.Pages[0].Name);
        Assert.False(File.Exists(_dir.File("board.json")));
        Assert.False(File.Exists(_dir.File("settings.json")));
    }

    [Fact]
    public void 廃止したhardwareAccelerationが残った設定も読め_次の保存で消える()
    {
        File.WriteAllText(_dir.File("settings.json"),
            """{ "schemaVersion": 3, "advanced": { "logging": true, "hardwareAcceleration": false } }""");
        var store = NewStore();
        var (settings, _) = store.LoadAll();
        Assert.Equal(LoadStatus.Loaded, settings.Status);
        Assert.True(settings.Value.Advanced.Logging);

        store.SaveSettings(settings.Value);
        Assert.DoesNotContain("hardwareAcceleration", File.ReadAllText(_dir.File("settings.json")));
    }

    [Fact]
    public void 盤面の既定の行列数は設定に従う()
    {
        var store = NewStore();
        var s = new AppSettings();
        s.Appearance.DefaultRows = 3;
        s.Appearance.DefaultCols = 8;
        store.SaveSettings(s);

        var (_, board) = NewStore().LoadAll();

        Assert.Equal(3, board.Value.Pages[0].Rows);
        Assert.Equal(8, board.Value.Pages[0].Cols);
    }

    [Fact]
    public void 保存して読み直すと全属性が戻る()
    {
        var store = NewStore();
        store.LoadAll();
        var board = Board.CreateDefault();
        var item = SampleItem();
        board.Pages[0].Items.Add(item);
        board.Pages[0].ButtonSize = 64;
        store.SaveBoard(board);

        var (_, loaded) = NewStore().LoadAll();

        Assert.Equal(LoadStatus.Loaded, loaded.Status);
        var page = Assert.Single(loaded.Value.Pages);
        Assert.Equal(64, page.ButtonSize);
        var got = Assert.Single(page.Items);
        Assert.Equal(item.Id, got.Id);
        Assert.Equal((1, 2), (got.Row, got.Col));
        Assert.Equal(ItemKind.App, got.Kind);
        Assert.Equal(item.Target, got.Target);
        Assert.Equal("メモ帳", got.Name);
        Assert.Equal("%1", got.Args);
        Assert.Equal(item.LinkPath, got.LinkPath);
        Assert.Equal("N", got.Hotkey);
        Assert.Null(got.WorkingDir);
    }

    [Fact]
    public void JSONは人間可読_camelCase_enumは文字列_日本語はそのまま_環境変数は展開しない()
    {
        var store = NewStore();
        store.LoadAll();
        var board = Board.CreateDefault();
        board.Pages[0].Items.Add(SampleItem());
        store.SaveBoard(board);

        string json = File.ReadAllText(_dir.File("board.json"));

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"kind\": \"app\"", json);
        Assert.Contains("\"launchMode\": \"normal\"", json);
        Assert.Contains("\"workingDir\": null", json);
        Assert.Contains("メモ帳", json);
        Assert.Contains("%WINDIR%", json);
        Assert.Contains("\n", json); // インデントあり
    }

    [Fact]
    public void 保存後に一時ファイルが残らない()
    {
        var store = NewStore();
        store.LoadAll();
        store.SaveBoard(Board.CreateDefault());
        store.SaveBoard(Board.CreateDefault());

        Assert.Empty(Directory.GetFiles(_dir.Path, "*.tmp"));
    }

    [Fact]
    public void 保存のたびに直前の内容を世代バックアップし_設定の世代数を超えた分は消す()
    {
        var store = NewStore();
        var s = new AppSettings();
        s.Data.BackupGenerations = 3;
        store.SaveSettings(s);
        store.LoadAll();

        for (int i = 0; i < 6; i++)
        {
            var b = Board.CreateDefault();
            b.Pages[0].Name = $"v{i}";
            store.SaveBoard(b);
            _time.Advance(AppDataStore.BoardBackupInterval); // 盤面のバックアップは 10 分おき（SPEC §9.3）
        }

        var backups = Directory.GetFiles(_dir.File("backup"), "board-*.json").OrderBy(f => f).ToList();
        Assert.Equal(3, backups.Count);
        // 初回保存時は本体が無いのでバックアップ無し → v0..v4 のうち新しい 3 つ（v2, v3, v4）が残る
        Assert.Contains("\"v2\"", File.ReadAllText(backups[0]));
        Assert.Contains("\"v4\"", File.ReadAllText(backups[2]));
    }

    [Fact]
    public void 同じミリ秒に2回保存してもバックアップが上書きされない()
    {
        // 時刻名の衝突を見るため、間隔を空けない（既定 0）ストアを直接使う
        var store = new VersionedJsonStore<Board>(_dir.File("board.json"), Board.CurrentSchemaVersion, () => Board.CreateDefault(),
            backupDirectory: _dir.File("backup"), time: _time);
        store.Save(Board.CreateDefault());
        store.Save(Board.CreateDefault());
        store.Save(Board.CreateDefault());

        Assert.Equal(2, Directory.GetFiles(_dir.File("backup"), "board-*.json").Length);
    }

    [Fact]
    public void 壊れた本体は退避して最新の正常なバックアップから復旧する()
    {
        var store = NewStore();
        store.LoadAll();
        foreach (var name in new[] { "old", "good", "latest" })
        {
            var b = Board.CreateDefault();
            b.Pages[0].Name = name;
            store.SaveBoard(b);
            _time.Advance(AppDataStore.BoardBackupInterval); // 盤面のバックアップは 10 分おき（SPEC §9.3）
        }
        // 最新バックアップ（= "good"）も壊す → その前の "old" から復旧されるはず
        var newestBackup = Directory.GetFiles(_dir.File("backup")).OrderBy(f => f).Last();
        File.WriteAllText(newestBackup, "{ broken");
        File.WriteAllText(_dir.File("board.json"), "{ \"pages\": [ 壊れた");

        var (_, board) = NewStore().LoadAll();

        Assert.Equal(LoadStatus.RecoveredFromBackup, board.Status);
        Assert.Equal("old", board.Value.Pages[0].Name);
        Assert.Single(Directory.GetFiles(_dir.Path, "board.json.broken-*"));
        // 本体も復旧内容で書き直されている
        Assert.Equal(LoadStatus.Loaded, NewStore().LoadAll().Board.Status);
    }

    [Fact]
    public void 壊れていてバックアップも無ければ既定値_壊れたファイルは残す()
    {
        File.WriteAllText(_dir.File("board.json"), "not json");

        var (_, board) = NewStore().LoadAll();

        Assert.Equal(LoadStatus.CorruptReplacedWithDefault, board.Status);
        Assert.Single(board.Value.Pages);
        var broken = Assert.Single(Directory.GetFiles(_dir.Path, "board.json.broken-*"));
        Assert.Equal("not json", File.ReadAllText(broken));
    }

    [Fact]
    public void 新しい版のデータは読み取り専用にして上書きしない()
    {
        const string newer = """{ "schemaVersion": 99, "pages": [], "futureField": true }""";
        File.WriteAllText(_dir.File("board.json"), newer);
        var store = NewStore();

        var (_, board) = store.LoadAll();

        Assert.Equal(LoadStatus.NewerSchemaReadOnly, board.Status);
        Assert.True(store.BoardStore.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => store.SaveBoard(board.Value));
        Assert.Equal(newer, File.ReadAllText(_dir.File("board.json")));
    }

    [Fact]
    public void schemaVersionが無い手書きJSONはv1として読む_コメントと末尾カンマも許す()
    {
        File.WriteAllText(_dir.File("board.json"), """
            {
              // 手編集
              "pages": [ { "name": "手書き", "rows": 2, "cols": 3, "items": [], }, ],
            }
            """);

        var (_, board) = NewStore().LoadAll();

        Assert.Equal(LoadStatus.Loaded, board.Status);
        Assert.Equal("手書き", board.Value.Pages[0].Name);
        Assert.NotEqual(Guid.Empty, board.Value.Pages[0].Id);
    }

    [Fact]
    public void 範囲外の行列数は丸めるがアイテムは消さない()
    {
        File.WriteAllText(_dir.File("board.json"), """
            { "schemaVersion": 1, "pages": [ { "name": "P", "rows": 0, "cols": 99,
              "items": [ { "row": 5, "col": 50, "kind": "url", "target": "https://example.com", "name": "x" } ] } ] }
            """);

        var (_, board) = NewStore().LoadAll();

        var page = board.Value.Pages[0];
        Assert.Equal(1, page.Rows);
        Assert.Equal(12, page.Cols);
        Assert.Single(page.Items);
    }

    [Fact]
    public void 設定の既定値はSPECどおり()
    {
        var s = NewStore().LoadAll().Settings.Value;

        Assert.Equal(DisplayMode.Popup, s.General.DisplayMode);
        Assert.True(s.Triggers.Hotkey.Enabled);
        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Alt, s.Triggers.Hotkey.Modifiers);
        Assert.Equal("Space", s.Triggers.Hotkey.Key);
        var mouse = Assert.Single(s.Triggers.Mouse);
        Assert.Equal((MouseGesture.Click, MouseButtonKind.Middle, KeyModifiers.Ctrl, true),
            (mouse.Gesture, mouse.Button, mouse.Modifiers, mouse.Enabled));
        Assert.Equal(48, s.Appearance.ButtonSize);
        Assert.Equal((4, 6), (s.Appearance.DefaultRows, s.Appearance.DefaultCols));
        Assert.True(s.Appearance.ImageThumbnails);
        Assert.Equal((PopupPosition.Cursor, PopupPosition.Cursor), (s.Popup.Keyboard.Position, s.Popup.Mouse.Position));
        Assert.Null(s.Popup.Keyboard.X);
        Assert.Equal(3, s.SchemaVersion);
        Assert.True(s.Popup.CloseOnLaunch && s.Popup.CloseOnOutsideClick && s.Popup.ToggleOnTrigger);
        Assert.False(s.Popup.CloseOnMouseLeave);
        Assert.Equal(10, s.Data.BackupGenerations);
    }

    [Fact]
    public void 旧形式v1の表示位置は読み込み時にキーボードとマウスの両方へ引き継ぎ_保存すると最新の版になる()
    {
        File.WriteAllText(_dir.File("settings.json"), """
            { "schemaVersion": 1, "popup": { "position": "fixed", "x": 100, "y": 200, "closeOnLaunch": false } }
            """);
        var store = NewStore();

        var result = store.LoadAll().Settings;
        var s = result.Value;

        Assert.False(store.SettingsStore.IsReadOnly);
        Assert.Equal((PopupPosition.Fixed, 100, 200), (s.Popup.Keyboard.Position, s.Popup.Keyboard.X, s.Popup.Keyboard.Y));
        Assert.Equal((PopupPosition.Fixed, 100, 200), (s.Popup.Mouse.Position, s.Popup.Mouse.X, s.Popup.Mouse.Y));
        Assert.False(s.Popup.CloseOnLaunch);

        store.SaveSettings(s);
        var saved = JsonNode.Parse(File.ReadAllText(_dir.File("settings.json")))!;
        Assert.Equal(3, (int)saved["schemaVersion"]!);
        Assert.Null(saved["popup"]!["position"]);
        Assert.Equal("fixed", (string)saved["popup"]!["keyboard"]!["position"]!);
    }

    [Fact]
    public void 旧形式v1で表示位置の指定が無ければ既定値で読む()
    {
        File.WriteAllText(_dir.File("settings.json"), """{ "schemaVersion": 1, "popup": { "closeOnLaunch": false } }""");
        var s = NewStore().LoadAll().Settings.Value;

        Assert.Equal((PopupPosition.Cursor, PopupPosition.Cursor), (s.Popup.Keyboard.Position, s.Popup.Mouse.Position));
        Assert.False(s.Popup.CloseOnLaunch);
    }

    [Fact]
    public void 修飾キーのフラグはJSONで読める形になる()
    {
        var store = NewStore();
        store.LoadAll();
        store.SaveSettings(new AppSettings());

        var node = JsonNode.Parse(File.ReadAllText(_dir.File("settings.json")))!;

        Assert.Equal("ctrl, alt", node["triggers"]!["hotkey"]!["modifiers"]!.GetValue<string>());
    }
}

public class AppSettingsTests
{
    [Fact]
    public void 盤面の不透明度の既定は80パーセント()
    {
        Assert.Equal(80, new AppSettings().Appearance.Opacity);
    }

    [Theory]
    [InlineData(10, 30)]
    [InlineData(30, 30)]
    [InlineData(62, 60)]
    [InlineData(63, 65)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void 不透明度は30から100の5刻みに丸める(int saved, int expected)
    {
        var s = new AppSettings();
        s.Appearance.Opacity = saved;

        s.Normalize();

        Assert.Equal(expected, s.Appearance.Opacity);
    }
}
