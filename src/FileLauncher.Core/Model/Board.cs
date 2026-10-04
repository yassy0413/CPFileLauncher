namespace FileLauncher.Core.Model;

/// <summary>盤面（SPEC §2, §9.3）。board.json のルート。</summary>
public sealed class Board
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Page> Pages { get; set; } = new();

    /// <summary>初回起動時の盤面: 「Page 1」が 1 枚（SPEC §6.5）。</summary>
    public static Board CreateDefault(int rows = Page.DefaultRows, int cols = Page.DefaultCols) => new()
    {
        Pages = { new Page { Name = "Page 1", Rows = rows, Cols = cols } },
    };

    /// <summary>読み込み後の正規化。手編集で範囲外になった値を丸める（アイテムは消さない）。</summary>
    public void Normalize()
    {
        if (Pages.Count == 0) Pages.Add(new Page { Name = "Page 1" });
        foreach (var page in Pages) page.Normalize();
    }
}

/// <summary>ページ = スロットを行×列に並べた 1 面（SPEC §2, §3.1, §6.5）。</summary>
public sealed class Page
{
    public const int DefaultRows = 4;
    public const int DefaultCols = 6;
    public const int MinGrid = 1;
    public const int MaxGrid = 12;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Page";
    public int Rows { get; set; } = DefaultRows;
    public int Cols { get; set; } = DefaultCols;

    /// <summary>ページ個別のボタンサイズ(px)。null は盤面共通設定を継承（SPEC §6.5）。</summary>
    public int? ButtonSize { get; set; }

    public List<LauncherItem> Items { get; set; } = new();

    internal void Normalize()
    {
        Rows = Math.Clamp(Rows, MinGrid, MaxGrid);
        Cols = Math.Clamp(Cols, MinGrid, MaxGrid);
        if (Id == Guid.Empty) Id = Guid.NewGuid();
        foreach (var item in Items)
        {
            if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
            item.Args ??= string.Empty;
        }
        // 手編集などで行列の範囲外になったアイテムは空きへ詰め直す（消さない。SPEC §6.5）
        FileLauncher.Core.Items.BoardEditing.ReflowOutOfRange(this, allowGrow: true);
    }
}

/// <summary>アイテム = 起動対象 1 件とその起動設定（SPEC §5.1）。</summary>
public sealed class LauncherItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Row { get; set; }
    public int Col { get; set; }
    public ItemKind Kind { get; set; }

    /// <summary>パス / URL / コマンド。環境変数は保存時に展開しない（起動時に展開。SPEC §9.3）。</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>表示名。既定は拡張子なしのファイル名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>コマンドライン引数。%1 / $1 はドロップされたパスに展開（SPEC §5.3）。</summary>
    public string Args { get; set; } = string.Empty;

    /// <summary>作業ディレクトリ。null は対象の親フォルダ。</summary>
    public string? WorkingDir { get; set; }

    /// <summary>ユーザー指定のアイコン画像。null は OS から自動取得（SPEC §5.4）。</summary>
    public string? IconOverride { get; set; }

    public LaunchMode LaunchMode { get; set; } = LaunchMode.Normal;

    /// <summary>管理者として実行（Windows のみ）。</summary>
    public bool RunAsAdmin { get; set; }

    /// <summary>盤面表示中に押すと起動する 1 キー（英数字）。</summary>
    public string? Hotkey { get; set; }

    /// <summary>.lnk から登録した場合の元の .lnk パス（SPEC §5.1）。アイコンは .lnk 自身から取る（SPEC §5.4）。</summary>
    public string? LinkPath { get; set; }

    /// <summary>分類用の色（スロット左端の縦帯。SPEC §3.6「アイテムの色帯」）。null はなし。全テーマで表示する。</summary>
    public ItemColor? Color { get; set; }
}

/// <summary>アイテムの色のパレット（8 色固定。表示色はテーマのトークン FlItemColor&lt;名前&gt;）。</summary>
public enum ItemColor
{
    Red,
    Orange,
    Yellow,
    Green,
    Cyan,
    Blue,
    Purple,
    Pink,
}

public enum ItemKind
{
    File,
    Folder,
    App,
    Url,
    Command,
}

/// <summary>起動時のウィンドウ状態（Windows のみ有効）。</summary>
public enum LaunchMode
{
    Normal,
    Minimized,
    Maximized,
}
