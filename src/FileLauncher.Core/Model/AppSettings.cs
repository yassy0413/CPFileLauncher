using FileLauncher.Core.Effects;

namespace FileLauncher.Core.Model;

/// <summary>アプリ設定（SPEC §7）。settings.json のルート。既定値は SPEC の記述に合わせる。</summary>
public sealed class AppSettings
{
    /// <summary>2: popup の表示位置をキーボード / マウスの 2 組に分けた（2026-10-03。SPEC §9.3）。</summary>
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public PopupSettings Popup { get; set; } = new();
    public TriggerSettings Triggers { get; set; } = new();
    public ResidentSettings Resident { get; set; } = new();
    public EditingSettings Editing { get; set; } = new();
    public BoardSettings Board { get; set; } = new();
    public DataSettings Data { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();

    public void Normalize()
    {
        General ??= new();
        if (General.Language is not ("system" or "ja" or "en")) General.Language = "system";
        Appearance ??= new();
        Popup ??= new();
        Triggers ??= new();
        Resident ??= new();
        Editing ??= new();
        Board ??= new();
        Data ??= new();
        Advanced ??= new();

        // 30〜100% を 5 刻み（SPEC §3.1、spec/SETTINGS.md）
        Appearance.Opacity = (int)Math.Round(Math.Clamp(Appearance.Opacity, 30, 100) / 5.0, MidpointRounding.AwayFromZero) * 5;
        Appearance.DefaultRows = Math.Clamp(Appearance.DefaultRows, Page.MinGrid, Page.MaxGrid);
        Appearance.DefaultCols = Math.Clamp(Appearance.DefaultCols, Page.MinGrid, Page.MaxGrid);
        if (!AppearanceSettings.ButtonSizes.Contains(Appearance.ButtonSize)) Appearance.ButtonSize = 48;
        Appearance.Vsync = Math.Clamp(Appearance.Vsync, 1, 3);
        Appearance.Background ??= new();
        Appearance.Background.Overlay = (int)Math.Round(Math.Clamp(Appearance.Background.Overlay, 0, 90) / 5.0, MidpointRounding.AwayFromZero) * 5;
        Appearance.Background.ImageOpacity = (int)Math.Round(Math.Clamp(Appearance.Background.ImageOpacity, 10, 100) / 5.0, MidpointRounding.AwayFromZero) * 5;
        if (string.IsNullOrWhiteSpace(Appearance.Background.Image)) Appearance.Background.Image = null;
        Popup.Keyboard ??= new();
        Popup.Mouse ??= new();
        Triggers.Hotkey ??= new();
        Triggers.Mouse ??= new();
        Triggers.LongPressMs = Math.Clamp(Triggers.LongPressMs, 200, 1000);
        Triggers.SimultaneousWindowMs = Math.Clamp(Triggers.SimultaneousWindowMs, 50, 200);
        Data.BackupGenerations = Math.Clamp(Data.BackupGenerations, 0, 100);
        Appearance.Effects = EffectCatalog.Normalize(Appearance.Effects);
        Appearance.Colors ??= new();
        Appearance.Colors.Normalize();
        Appearance.Hud ??= new();
        Appearance.Hud.GridOpacity = Math.Clamp(Appearance.Hud.GridOpacity, 0, 10);
        Appearance.Hud.ScanlineOpacity = (int)Math.Round(Math.Clamp(Appearance.Hud.ScanlineOpacity, 0, 50) / 5.0, MidpointRounding.AwayFromZero) * 5;
        if (!HudSettings.ScanlinePitches.Contains(Appearance.Hud.ScanlinePitch)) Appearance.Hud.ScanlinePitch = 3;
    }
}

public sealed class GeneralSettings
{
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Popup;
    public bool AutoStart { get; set; }

    /// <summary>"system" / "ja" / "en"</summary>
    public string Language { get; set; } = "system";

    /// <summary>フォルダを開く先（Windows のみ。macOS では無視。SPEC §5.2「フォルダを開く先」）。</summary>
    public FolderOpenTarget FolderOpenTarget { get; set; } = FolderOpenTarget.ExistingTab;
}

public enum DisplayMode
{
    Popup,
    Resident,
}

/// <summary>フォルダの開き方（SPEC §5.2）。</summary>
public enum FolderOpenTarget
{
    /// <summary>開いている Explorer のいちばん手前のウィンドウに新しいタブを足す（無ければ新しいウィンドウ）。</summary>
    ExistingTab,
    NewWindow,
    /// <summary>従来の ShellExecute（OS の設定に任せる）。</summary>
    System,
}

public sealed class AppearanceSettings
{
    /// <summary>選択できるボタンサイズ（小 / 中 / 大 / 特大、SPEC §3.1）。</summary>
    public static readonly int[] ButtonSizes = [32, 48, 64, 96];

    /// <summary>常時の演出の更新間隔（VSync）。1 = 60 fps / 2 = 30 fps / 3 = 20 fps（2026-10-07 ユーザー要望。spec/EFFECTS.md「時計」）。</summary>
    public int Vsync { get; set; } = 2;

    public static readonly int[] VsyncValues = [1, 2, 3];

    /// <summary>配色（主色・副色。SPEC §3.6「配色」）。見た目はサイバーパンク専用（テーマの選択は 2026-10-04 に廃止）。</summary>
    public ColorSettings Colors { get; set; } = new();

    /// <summary>HUD（ステータス行・時刻・背景グリッド。SPEC §3.9）。</summary>
    public HudSettings Hud { get; set; } = new();

    /// <summary>盤面の不透明度 %（30〜100、5 刻み。既定 80）。</summary>
    public int Opacity { get; set; } = 80;

    public int ButtonSize { get; set; } = 48;
    public LabelMode Label { get; set; } = LabelMode.Below;
    public int DefaultRows { get; set; } = Page.DefaultRows;
    public int DefaultCols { get; set; } = Page.DefaultCols;
    /// <summary>全演出の ON/OFF（spec/EFFECTS.md）。OFF なら全部「なし」。</summary>
    public bool Animation { get; set; } = true;

    /// <summary>既定と違う演出だけ（キーは EffectCatalog の ID）。既定の値はコード内の EffectCatalog。</summary>
    public Dictionary<string, EffectSpec> Effects { get; set; } = new();

    /// <summary>画像ファイルはサムネイルで表示（SPEC §5.4、既定 ON）。</summary>
    public bool ImageThumbnails { get; set; } = true;

    /// <summary>盤面の背景画像（SPEC §3.7。全ページ共通）。</summary>
    public BackgroundSettings Background { get; set; } = new();
}

/// <summary>盤面の背景画像（SPEC §3.7、spec/SETTINGS.md 表示タブ）。</summary>
public sealed class BackgroundSettings
{
    /// <summary>null = なし。データフォルダからの相対パス "background/&lt;名前&gt;"（設定画面で選んだとき）または絶対パス（手編集）。</summary>
    public string? Image { get; set; }

    public BackgroundFit Fit { get; set; } = BackgroundFit.Fill;

    /// <summary>画像の上に重ねる面の色の不透明度 %（0〜90、5 刻み。既定 40）。</summary>
    public int Overlay { get; set; } = 40;

    /// <summary>画像の不透明度 %（10〜100、5 刻み。既定 100）。画像があるときは地の色を描かないので、下げると画像の部分からデスクトップが透ける。</summary>
    public int ImageOpacity { get; set; } = 100;
}

/// <summary>背景画像の表示方法（埋める / 収める / 引き伸ばす / 並べる / 中央）。</summary>
public enum BackgroundFit
{
    Fill,
    Fit,
    Stretch,
    Tile,
    Center,
}

/// <summary>配色（SPEC §3.6「配色」）。#RRGGBB。背景・文字は主色から導く（Core.Theming.CyberDerivation）。</summary>
public sealed class ColorSettings
{
    public string Primary { get; set; } = "#00E5FF";
    public string Secondary { get; set; } = "#FF2BD6";

    /// <summary>
    /// 読めない色はシアンのプリセットに戻し、表記を大文字の #RRGGBB にそろえる。直したら true（App がログに 1 行出す）。
    /// 暗い色を自動で明るくする補正はしない（カラーピッカーの操作中に値が勝手に変わるのが使いにくい。2026-10-04 ユーザー判断）。
    /// </summary>
    public bool Normalize()
    {
        var (defP, defS) = Theming.ColorPresets.For(AccentPreset.Cyan);
        var p = Theming.RgbColor.TryParse(Primary);
        var s = Theming.RgbColor.TryParse(Secondary);
        bool changed = p is null || s is null;
        Primary = (p ?? defP).ToHex();
        Secondary = (s ?? defS).ToHex();
        return changed;
    }
}

/// <summary>HUD（SPEC §3.9）。</summary>
public sealed class HudSettings
{
    /// <summary>盤面の下のステータス行（PAGE / ITEMS / 時刻）。</summary>
    public bool StatusBar { get; set; } = true;
    public ClockMode Clock { get; set; } = ClockMode.Minutes;

    /// <summary>時刻の前に日付（yyyy/MM/dd）も出す（2026-10-04 ユーザー要望）。</summary>
    public bool Date { get; set; } = true;

    /// <summary>盤面の背景の細いグリッド。</summary>
    public bool Grid { get; set; } = true;

    /// <summary>グリッドの線の濃さ %（主色の不透明度。0〜10、既定 3。2026-10-04 ユーザー要望）。</summary>
    public int GridOpacity { get; set; } = 3;

    /// <summary>静止した走査線（主色の横線。SPEC §3.9 H14。2026-10-07 ユーザーが実物で選んで採用）。</summary>
    public bool Scanlines { get; set; } = true;

    /// <summary>走査線の濃さ %（0〜50、5 刻み、既定 25）。</summary>
    public int ScanlineOpacity { get; set; } = 25;

    /// <summary>走査線の間隔 px（<see cref="ScanlinePitches"/> のどれか、既定 3）。</summary>
    public int ScanlinePitch { get; set; } = 3;

    /// <summary>選択できる走査線の間隔（px）。</summary>
    public static readonly int[] ScanlinePitches = [2, 3, 4];
}

public enum ClockMode
{
    Off,
    Minutes,
    Seconds,
}

/// <summary>サイバーパンクの基調色プリセット（主色 / 副色。色の値は App 層 CyberPalette）。</summary>
/// <summary>配色のプリセットの ID（Core.Theming.ColorPresets。JSON には書かない）。</summary>
public enum AccentPreset
{
    Cyan,
    Red,
    Blue,
    Green,
    Purple,
}

public enum LabelMode
{
    None,
    Below,
    Hover,
}

public sealed class PopupSettings
{
    /// <summary>ホットキー（キーボード）で開いたときの表示位置（SPEC §3.3）。</summary>
    public PopupPlacementSettings Keyboard { get; set; } = new();

    /// <summary>マウストリガー・トレイ・メニュー・2 重起動で開いたときの表示位置。</summary>
    public PopupPlacementSettings Mouse { get; set; } = new();

    public bool CloseOnLaunch { get; set; } = true;
    public bool CloseOnOutsideClick { get; set; } = true;
    public bool ToggleOnTrigger { get; set; } = true;
    public bool CloseOnMouseLeave { get; set; }
    public int MouseLeaveDistance { get; set; } = 200;

    public PopupPlacementSettings PlacementFor(PopupTriggerKind kind) =>
        kind == PopupTriggerKind.Keyboard ? Keyboard : Mouse;

    /// <summary>
    /// 閉じたときの位置を「前回の位置」を選んでいる組すべてに書く（SPEC §3.3）。両方 lastPosition なら 1 つの記憶として振る舞い、
    /// 片方だけなら、もう片方で開いた場所に上書きされない。fixed の組の座標は変えない。1 つでも書いたら true。
    /// </summary>
    public bool RememberLastPosition(int x, int y)
    {
        bool any = false;
        foreach (var p in new[] { Keyboard, Mouse })
        {
            if (p.Position != PopupPosition.LastPosition) continue;
            p.X = x;
            p.Y = y;
            any = true;
        }
        return any;
    }
}

/// <summary>表示位置 1 組（SPEC §3.3）。X / Y は Fixed の座標、または LastPosition の前回位置（Win: 物理 px、Mac: pt）。</summary>
public sealed class PopupPlacementSettings
{
    public PopupPosition Position { get; set; } = PopupPosition.Cursor;
    public int? X { get; set; }
    public int? Y { get; set; }
}

/// <summary>盤面を開いた操作の種類（表示位置の組を選ぶ）。</summary>
public enum PopupTriggerKind
{
    Keyboard,
    Mouse,
}

public enum PopupPosition
{
    Cursor,
    LastPosition,
    ScreenCenter,
    Fixed,
}

public sealed class TriggerSettings
{
    public HotkeySetting Hotkey { get; set; } = new();

    /// <summary>有効にするマウス操作（複数可）。既定は Ctrl+中クリックのみ（SPEC §4.2）。</summary>
    public List<MouseTriggerSetting> Mouse { get; set; } =
    [
        new() { Gesture = MouseGesture.Click, Button = MouseButtonKind.Middle, Modifiers = KeyModifiers.Ctrl },
    ];

    /// <summary>マウス操作をデスクトップ（壁紙）上でのみ有効にする。</summary>
    public bool DesktopOnly { get; set; }

    public int LongPressMs { get; set; } = 400;
    public int SimultaneousWindowMs { get; set; } = 100;
}

/// <summary>グローバルホットキー。既定 Ctrl+Alt+Space（macOS は ⌃⌥Space。Ctrl=⌃, Alt=⌥, Meta=Win/⌘）。</summary>
public sealed class HotkeySetting
{
    public bool Enabled { get; set; } = true;
    public KeyModifiers Modifiers { get; set; } = KeyModifiers.Ctrl | KeyModifiers.Alt;

    /// <summary>キー名（"Space", "A", "F1" など）。</summary>
    public string Key { get; set; } = "Space";
}

public sealed class MouseTriggerSetting
{
    public bool Enabled { get; set; } = true;
    public MouseGesture Gesture { get; set; }
    public MouseButtonKind Button { get; set; } = MouseButtonKind.Middle;
    public KeyModifiers Modifiers { get; set; }
}

/// <summary>マウス操作の種類（SPEC §4.2）。</summary>
public enum MouseGesture
{
    /// <summary>（修飾キー +）ボタンクリック。</summary>
    Click,

    /// <summary>ボタン長押し。</summary>
    LongPress,

    /// <summary>左右ボタン同時クリック（Button は無視）。</summary>
    LeftRightTogether,

    /// <summary>中ボタンを押しながらホイール回転。</summary>
    WheelClickRotate,
}

public enum MouseButtonKind
{
    Left,
    Middle,
    X1,
    X2,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Meta = 8,
}

public sealed class ResidentSettings
{
    public ZOrder ZOrder { get; set; } = ZOrder.Normal;
    public bool AutoHide { get; set; }
    public bool LockPosition { get; set; }

    /// <summary>トリガーで盤面をカーソル位置に移動させる（SPEC §3.4）。</summary>
    public bool MoveToCursorOnTrigger { get; set; }

    /// <summary>前回の位置・サイズ（物理 px）。null は初回。</summary>
    public WindowBounds? Bounds { get; set; }
}

public enum ZOrder
{
    Topmost,
    Normal,
    Bottommost,
}

public sealed record WindowBounds(int X, int Y, int Width, int Height);

/// <summary>盤面編集時の確認（SPEC §5.3, §6.2）。</summary>
public sealed class EditingSettings
{
    /// <summary>フォルダアイテムへ Shift+ドロップで移動するときに確認する。</summary>
    public bool ConfirmMoveOnDrop { get; set; } = true;
}

/// <summary>盤面の操作（SPEC §6.5 ページの切替）。</summary>
public sealed class BoardSettings
{
    /// <summary>盤面の上のホイール回転でページを切り替える。</summary>
    public bool WheelSwitchesPage { get; set; } = true;

    /// <summary>Alt+1〜9（Mac は ⌥1〜9）でページを切り替える。</summary>
    public bool AltNumberSwitchesPage { get; set; } = true;
}

public sealed class DataSettings
{
    /// <summary>board.json の世代バックアップ数（SPEC §9.2、既定 10）。</summary>
    public int BackupGenerations { get; set; } = 10;
}

public sealed class AdvancedSettings
{
    public bool Logging { get; set; }
    // hardwareAcceleration は 2026-10-07 に廃止（描画方式はコードで固定。SPEC §10.7）。残ったキーは読み込みで無視される
}
