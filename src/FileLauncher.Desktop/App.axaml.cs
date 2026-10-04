using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;
using FileLauncher.Platform;
using FileLauncher.Platform.MacOS;
using FileLauncher.Platform.Windows;

namespace FileLauncher.App;

public partial class App : Application
{
    private IPlatformServices? _platform;
    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private BoardWindow? _board;
    private NativeMenuItem? _permissionItem;
    private PermissionGuideWindow? _permissionGuide;
    private DispatcherTimer? _permissionPoll;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private BoardEditor? _editor;
    private SettingsHub? _hub;
    private PopupController? _popup;
    private ResidentController? _resident;
    private IBoardController? _current;
    private Board? _boardData;
    private NativeMenuItem? _modePopupItem;
    private NativeMenuItem? _modeResidentItem;
    private SettingsWindow? _settingsWindow;
    private DataPaths? _paths;
    private AppDataStore? _store;
    private readonly BoardBackgroundLoader _backgroundLoader = new();
    private CancellationTokenSource? _backgroundCts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppTheme.Install(this); // テーマのトークン（FluentTheme の後）
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // トレイ常駐アプリ: ウィンドウを全部閉じても終了しない。終了はトレイメニューから
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Start(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var store = _store = new AppDataStore(_paths = DataPaths.Resolve(DataPaths.ExecutableDirectory(Environment.ProcessPath, AppContext.BaseDirectory)));

        // 2 重起動なら既存インスタンスに「盤面を表示」を送って終了
        _instance = new SingleInstance(store.Paths.Root);
        if (!_instance.TryAcquire())
        {
            _instance.SignalExisting();
            _instance.Dispose();
            _instance = null;
            desktop.Shutdown();
            return;
        }

        var (settingsResult, boardResult) = store.LoadAll();
        var settings = settingsResult.Value;
        var board = boardResult.Value;
        AppLog.Init(store.Paths.LogsDirectory, settings.Advanced.Logging);
        AppLog.Info($"起動: data={store.Paths.Root}{(store.Paths.IsPortable ? " (portable)" : "")} settings={settingsResult.Status} board={boardResult.Status} hw={settings.Advanced.HardwareAcceleration} lang={Loc.StartupLanguage}({settings.General.Language})");
        if (DataPaths.LastMigration != LegacyMigration.None) AppLog.Info($"旧データフォルダ（{DataPaths.LegacyFolderName}）の引き継ぎ: {DataPaths.LastMigration}");

        if (settingsResult.Status == LoadStatus.CreatedDefault) store.SaveSettings(settings);
        if (boardResult.Status == LoadStatus.CreatedDefault) store.SaveBoard(board);
        NotifyLoadProblems(Strings.Notice_Load_Settings, settingsResult);
        NotifyLoadProblems(Strings.Notice_Load_Board, boardResult);

        _hub = new SettingsHub(store, settings);
        var hub = _hub;
        Toast.Appearance = () => hub.Current.Appearance;
        ApplyColors(settings.Appearance.Colors); // 最初の描画の前に、配色で辞書を作り直す

        _desktop = desktop;
        if (OperatingSystem.IsWindows()) _platform = new WindowsPlatformServices();
        else if (OperatingSystem.IsMacOS()) _platform = new MacPlatformServices();
        else
        {
            Notice.Show(Strings.Notice_UnsupportedOs_Title, Strings.Notice_UnsupportedOs);
            return;
        }

        _board = new BoardWindow { Icons = new IconService(_platform.Icons, store.Paths.IconsDirectory) };
        _boardData = board;
        _board.ReducedMotion = () => _platform?.Window.PrefersReducedMotion == true;
        _board.ApplyEffects(settings.Appearance);
        ApplyWindowEffects(settings.Appearance);
        _board.Render(board, settings.Appearance, 0);
        _ = ApplyBackgroundAsync(); // 背景画像は後から（起動・初回表示を待たせない）
        var popup = _popup = new PopupController(_board, _platform, _hub, board);
        popup.Attach();
        var resident = _resident = new ResidentController(_board, _platform, _hub);
        resident.Attach();
        var editor = _editor = new BoardEditor(_board, () => _current ?? popup, _platform, store, _hub, board)
        {
            SetDisplayMode = mode => _hub?.Update(s => s.General.DisplayMode = mode, SettingsChange.DisplayMode),
            OpenSettings = OpenSettings,
            Quit = () => Quit(desktop),
            ShowAbout = () => AboutWindow.Show(_platform.Shell, store.Paths.Root, store.Paths.IsPortable),
        };
        editor.Attach();
        _board.SettingsRequested += OpenSettings;
        _hub.Changed += change => OnSettingsChanged(change, board);

        _platform.Hotkey.Register(settings.Triggers.Hotkey, out _);
        _platform.MouseTrigger.Configure(settings.Triggers);
        // フック開始に失敗しても設定画面・ガイドは勝手に開かない（SPEC §4.3）。トーストとトレイ / メニューバーの警告だけ
        _platform.InputHook.Failed += error => Dispatcher.UIThread.Post(() => OnHookFailed(error));

        _instance.Listen(() => Dispatcher.UIThread.Post(() => _current?.ShowFromExternal("2 重起動"/* i18n:ignore ログ用 */)));

        CreateTray(desktop);
        _platform.InputHook.Start();
        SwitchMode(settings.General.DisplayMode);
        desktop.Exit += (_, _) => Cleanup();
        // 起動時間（SPEC §11）: プロセス開始からトリガーを受け付けられるまで。最初の描画が落ち着いた時点も出す
        AppLog.Info($"起動完了: {StartupMs():F0} ms（トレイ・入力フック・盤面の準備まで）");
        Dispatcher.UIThread.Post(() => AppLog.Info($"起動後の最初のアイドル: {StartupMs():F0} ms"), DispatcherPriority.ApplicationIdle);
    }

    private static double StartupMs() =>
        (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds;

    // ---------------- 表示モード（SPEC §3.4） ----------------

    /// <summary>表示モードを切り替える（今のコントローラを無効にして盤面を隠し、新しいほうを有効に）。起動時にも呼ぶ。</summary>
    private async void SwitchMode(DisplayMode mode)
    {
        if (_popup is null || _resident is null) return;
        IBoardController next = mode == DisplayMode.Resident ? _resident : _popup;
        if (ReferenceEquals(next, _current)) return;
        bool startup = _current is null;
        var previous = _current;
        _current = next;
        _editor?.Flush();
        // 前のモードの非表示演出が終わってから新しいモードで出す（同じ盤面ウィンドウを使うため）
        if (previous is not null) await previous.DeactivateAsync();
        if (!ReferenceEquals(_current, next)) return; // 演出中にさらに切り替えられた
        next.Activate();
        if (next == _popup && startup) _popup.Warmup(); // 初回表示の重さ対策（ポップアップのみ）
        if (_modePopupItem is not null) _modePopupItem.IsChecked = mode == DisplayMode.Popup;
        if (_modeResidentItem is not null) _modeResidentItem.IsChecked = mode == DisplayMode.Resident;
        AppLog.Info($"表示モード: {mode}");
    }

    private void CreateTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var show = new NativeMenuItem(Strings.Tray_ShowBoard);
        show.Click += (_, _) => _current?.ShowFromExternal("トレイメニュー"); // i18n:ignore

        // 表示モードの即時切替（SPEC §3.4 / §8）。設定に保存するので設定画面とも同期する
        _modePopupItem = new NativeMenuItem(EnumNames.Of(DisplayMode.Popup)) { ToggleType = NativeMenuItemToggleType.Radio };
        _modeResidentItem = new NativeMenuItem(EnumNames.Of(DisplayMode.Resident)) { ToggleType = NativeMenuItemToggleType.Radio };
        _modePopupItem.Click += (_, _) => _hub?.Update(s => s.General.DisplayMode = DisplayMode.Popup, SettingsChange.DisplayMode);
        _modeResidentItem.Click += (_, _) => _hub?.Update(s => s.General.DisplayMode = DisplayMode.Resident, SettingsChange.DisplayMode);
        var modeItem = new NativeMenuItem(Strings.Tray_DisplayMode) { Menu = new NativeMenu { Items = { _modePopupItem, _modeResidentItem } } };

        // ページ一覧（選ぶとそのページで盤面を表示。SPEC §8）。開くたびに作り直す（ページ操作は M4）
        var pagesMenu = new NativeMenu();
        pagesMenu.NeedsUpdate += (_, _) => RebuildPagesMenu(pagesMenu);
        RebuildPagesMenu(pagesMenu);
        var pagesItem = new NativeMenuItem(Strings.Tray_Pages) { Menu = pagesMenu };
        var settingsItem = new NativeMenuItem(Strings.Common_Settings)
        {
            Gesture = new KeyGesture(Key.OemComma, OperatingSystem.IsMacOS() ? Avalonia.Input.KeyModifiers.Meta : Avalonia.Input.KeyModifiers.Control),
        };
        settingsItem.Click += (_, _) => OpenSettings();
        _permissionItem = new NativeMenuItem(Strings.Tray_Permissions) { IsVisible = false }; // 未許可のときだけ出す（Mac）
        _permissionItem.Click += (_, _) => ShowPermissionGuide();
        var quit = new NativeMenuItem(Loc.Os(Strings.Tray_Quit_Win, Strings.Tray_Quit_Mac)) { Gesture = OperatingSystem.IsMacOS() ? new KeyGesture(Key.Q, Avalonia.Input.KeyModifiers.Meta) : null };
        quit.Click += (_, _) => Quit(desktop);

        if (OperatingSystem.IsMacOS())
        {
            // アプリメニュー（盤面がキーのときに ⌘Q を効かせる）
            var appSettings = new NativeMenuItem(Strings.Common_Settings) { Gesture = new KeyGesture(Key.OemComma, Avalonia.Input.KeyModifiers.Meta) };
            appSettings.Click += (_, _) => OpenSettings();
            var appQuit = new NativeMenuItem(Strings.Tray_Quit_Mac) { Gesture = new KeyGesture(Key.Q, Avalonia.Input.KeyModifiers.Meta) };
            appQuit.Click += (_, _) => Quit(desktop);
            NativeMenu.SetMenu(this, new NativeMenu { Items = { appSettings, new NativeMenuItemSeparator(), appQuit } });
        }

        _tray = new TrayIcon
        {
            Icon = AppIcon.Create(),
            ToolTipText = AppInfo.ProductName,
            Menu = new NativeMenu { Items = { show, pagesItem, modeItem, new NativeMenuItemSeparator(), settingsItem, _permissionItem, new NativeMenuItemSeparator(), quit } },
            IsVisible = true,
        };
        MacOSProperties.SetIsTemplateIcon(_tray, AppIcon.IsTemplate(warning: false));
        _tray.Clicked += (_, _) => _current?.Toggle("トレイ"); // i18n:ignore
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    /// <summary>
    /// 終了。ウィンドウが片付けられる前に常駐の位置を保存して止める（片付け中の macOS はウィンドウ位置が 0,0 になり、
    /// それを保存して次回左上に出ていた。2026-10-03）。
    /// </summary>
    private async void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_current is { } current)
        {
            _current = null;
            await current.DeactivateAsync(); // 常駐なら位置を保存し、消える演出を見せてから終了
        }
        _editor?.Flush();
        _hub?.Flush();
        desktop.Shutdown();
    }

    private void RebuildPagesMenu(NativeMenu menu)
    {
        menu.Items.Clear();
        if (_boardData is null || _board is null || _hub is null) return;
        for (int i = 0; i < _boardData.Pages.Count; i++)
        {
            int index = i;
            var item = new NativeMenuItem(_boardData.Pages[i].Name)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = i == _board.PageIndex,
            };
            item.Click += (_, _) =>
            {
                _board.Render(_boardData, _hub.Current.Appearance, index);
                _current?.ShowFromExternal("ページ一覧"); // i18n:ignore
            };
            menu.Items.Add(item);
        }
    }

    // ---------------- 設定（SPEC §7） ----------------

    /// <summary>設定画面を開く（ユーザーが選んだときだけ。既に開いていれば前面へ）。盤面が出ていれば閉じる（SPEC §3.5）。</summary>
    private void OpenSettings()
    {
        if (_hub is null || _platform is null) return;
        _editor?.Flush();
        if (_current == _popup) _popup?.Hide("settings", restoreFocus: false); // 常駐の盤面は出したまま
        if (_settingsWindow is null)
        {
            var platform = _platform;
            _settingsWindow = new SettingsWindow(new SettingsContext(_hub, platform, _paths!,
                () => _current?.IsShown == true && _board is not null ? _board.Position : null,
                dir => platform.Shell.Launch(new LauncherItem { Kind = ItemKind.Folder, Target = dir, Name = Path.GetFileName(dir) }),
                ShowPermissionGuide,
                FlushAll: () => { _editor?.Flush(); _hub?.Flush(); },
                ImportAndRestart: ImportAndRestart,
                BackgroundStatus: () => _backgroundLoader.LastStatus,
                BackgroundBitmap: () => _backgroundLoader.Current,
                Restart: Restart,
                HardwareAccelerationAtStartup: Program.HardwareAccelerationAtStartup));
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        WindowActivation.ShowAndActivate(_settingsWindow, _platform);
    }

    /// <summary>設定が変わったら、関係する所だけ反映し直す（即時反映）。</summary>
    private void OnSettingsChanged(SettingsChange change, Board board)
    {
        if (_hub is null || _board is null || _platform is null) return;
        var s = _hub.Current;
        if (change.HasFlag(SettingsChange.Colors)) ApplyColors(s.Appearance.Colors);
        if (change.HasFlag(SettingsChange.Opacity)) _board.ApplyOpacity(s.Appearance.Opacity);
        // テーマを変えると演出の既定値と発光の余白（ウィンドウの大きさ）も変わる
        if (change.HasFlag(SettingsChange.Effects)) { _board.ApplyEffects(s.Appearance); ApplyWindowEffects(s.Appearance); }
        if (change.HasFlag(SettingsChange.BoardLayout) || change.HasFlag(SettingsChange.Hud)) _board.Render(board, s.Appearance, _board.PageIndex);
        if (change.HasFlag(SettingsChange.Triggers))
        {
            _platform.Hotkey.Register(s.Triggers.Hotkey, out _);
            _platform.MouseTrigger.Configure(s.Triggers);
        }
        if (change.HasFlag(SettingsChange.Logging)) AppLog.SetEnabled(s.Advanced.Logging);
        if (change.HasFlag(SettingsChange.DisplayMode)) SwitchMode(s.General.DisplayMode);
        if (change.HasFlag(SettingsChange.Resident)) _resident?.ApplySettings();
        if (change.HasFlag(SettingsChange.Background)) _ = ApplyBackgroundAsync();
    }

    /// <summary>背景画像を読み込んで盤面に敷く（SPEC §3.7）。続けて変わったら前の読み込みは捨てる。</summary>
    private async Task ApplyBackgroundAsync()
    {
        if (_hub is null || _board is null || _paths is null) return;
        _backgroundCts?.Cancel();
        var cts = _backgroundCts = new CancellationTokenSource();
        var bg = _hub.Current.Appearance.Background;
        try
        {
            var bitmap = await _backgroundLoader.LoadAsync(BackgroundImageStore.Resolve(_paths, bg.Image), cts.Token);
            if (cts.IsCancellationRequested) return;
            _board.ApplyBackground(_hub.Current.Appearance.Background, bitmap);
            if (bg.Image is not null) AppLog.Info($"背景画像: {bg.Image} → {_backgroundLoader.LastStatus}");
            _hub.NotifyBackgroundLoaded();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>設定画面・ダイアログも盤面と同じ表示・非表示の演出（グリッチ）で出し入れする。</summary>
    private static void ApplyWindowEffects(AppearanceSettings appearance)
    {
        ChromeWindow.ShowEffect = Core.Effects.EffectCatalog.Resolve(appearance, Core.Effects.EffectCatalog.BoardShow);
        ChromeWindow.HideEffect = Core.Effects.EffectCatalog.Resolve(appearance, Core.Effects.EffectCatalog.BoardHide);
    }

    /// <summary>配色（主色・副色。SPEC §3.6）を見た目に反映する。値は Normalize 済み（#RRGGBB）。</summary>
    private void ApplyColors(ColorSettings colors)
    {
        if (Core.Theming.RgbColor.TryParse(colors.Primary) is { } p && Core.Theming.RgbColor.TryParse(colors.Secondary) is { } s)
            AppTheme.SetColors(this, p, s);
    }

    // ---------------- 権限・フック失敗（SPEC §4.3） ----------------

    private void OnHookFailed(string error)
    {
        AppLog.Info($"入力フック開始失敗: {error}");
        if (_platform is null) return;
        SetTriggerWarning(true);

        bool needsPermission = _platform.Permissions.Accessibility != PermissionState.Granted;
        if (_permissionItem is not null) _permissionItem.IsVisible = needsPermission;
        Toast.Show(needsPermission
            ? Strings.Toast_TriggerNeedsPermission
            : Strings.FormatToast_TriggerHookFailed(Loc.Os(Strings.Common_TrayIcon_Win, Strings.Common_TrayIcon_Mac), error), seconds: 10);
        if (needsPermission) StartPermissionPolling();
    }

    /// <summary>許可されたら自動でフックを開始し直す（2 秒ごと）。成功したら警告を消して止める。</summary>
    private void StartPermissionPolling()
    {
        if (_permissionPoll is not null || _platform is null) return;
        var platform = _platform;
        _permissionPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _permissionPoll.Tick += (_, _) =>
        {
            if (platform.InputHook.IsRunning)
            {
                AppLog.Info("入力フック開始（権限許可後）");
                _permissionPoll?.Stop();
                _permissionPoll = null;
                SetTriggerWarning(false);
                if (_permissionItem is not null) _permissionItem.IsVisible = false;
                Toast.Show(Strings.Toast_TriggerEnabled, error: false);
                return;
            }
            if (platform.Permissions.Accessibility == PermissionState.Granted) platform.InputHook.Start();
        };
        _permissionPoll.Start();
    }

    private void SetTriggerWarning(bool warning)
    {
        if (_tray is null) return;
        _tray.Icon = AppIcon.Create(warning);
        MacOSProperties.SetIsTemplateIcon(_tray, AppIcon.IsTemplate(warning));
        _tray.ToolTipText = warning ? Strings.Tray_TriggerDisabled_Tooltip : AppInfo.ProductName;
    }

    private void ShowPermissionGuide()
    {
        if (_platform is null) return;
        if (_permissionGuide is null)
        {
            _permissionGuide = new PermissionGuideWindow(_platform, RetryHook, Restart);
            _permissionGuide.Closed += (_, _) => _permissionGuide = null;
        }
        // 常駐アプリ（Dock に出ない）なので明示的に前面化する。閉じたら直前のアプリへ返す（SPEC §3.5）
        WindowActivation.ShowAndActivate(_permissionGuide, _platform);
    }

    private void RetryHook()
    {
        AppLog.Info("入力フック再試行");
        _platform?.InputHook.Start();
        StartPermissionPolling();
    }

    /// <summary>エクスポートした zip で settings.json / board.json を差し替えて再起動する（spec/SETTINGS.md データタブ）。</summary>
    private void ImportAndRestart(ArchiveContents contents)
    {
        if (_store is null) return;
        _editor?.Flush();
        _hub?.Flush();
        try
        {
            _store.Import(contents, DateTime.Now); // 以後このプロセスは保存しない（終了処理で上書きしないため）
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toast.Show(Strings.FormatToast_ImportFailed(ex.Message));
            return;
        }
        AppLog.Info($"インポート: settings={contents.Settings is not null} board={contents.Board is not null}");
        Restart();
    }

    /// <summary>権限の反映に再起動が要る場合のため、自分をもう一度起動して終了する。</summary>
    private void Restart()
    {
        if (Environment.ProcessPath is not { } exe) return;
        _editor?.Flush(); // 再起動の前に保存待ちを書き切る（言語の変更など）
        _hub?.Flush();
        AppLog.Info("再起動");
        _instance?.Dispose(); // 新しいプロセスが単一インスタンスのロックを取れるように先に放す
        _instance = null;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false });
        _desktop?.Shutdown();
    }

    private static void NotifyLoadProblems<T>(string what, LoadResult<T> result)
    {
        var d = result.Detail ?? new LoadDetail();
        string? message = result.Status switch
        {
            LoadStatus.RecoveredFromBackup => Strings.FormatNotice_Load_Recovered(what, d.BackupFile, d.BrokenFile, d.Error),
            LoadStatus.CorruptReplacedWithDefault => Strings.FormatNotice_Load_ReplacedWithDefault(what, d.BrokenFile, d.Error),
            LoadStatus.NewerSchemaReadOnly => Strings.FormatNotice_Load_NewerSchema(what, d.FileVersion, d.SupportedVersion),
            _ => null,
        };
        if (message is null) return;
        AppLog.Info($"読み込みの問題: {what} {result.Status} {d}");
        Notice.Show(Strings.Notice_Load_Title, message);
    }

    private void Cleanup()
    {
        AppLog.Info("終了");
        _backgroundCts?.Cancel();
        _resident?.DeactivateAsync(); // 常駐の位置を保存してから（OS からの終了。演出は待たない）
        _editor?.Flush();
        _hub?.Flush();
        if (_tray is not null) _tray.IsVisible = false;
        if (_board is not null)
        {
            _board.AllowClose = true;
            _board.Close();
        }
        _platform?.Dispose();
        _instance?.Dispose();
    }
}
