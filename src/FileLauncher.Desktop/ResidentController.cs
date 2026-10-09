using System.Diagnostics;
using Avalonia;
using Avalonia.Threading;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Input;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Core.Popup;
using FileLauncher.Platform;

namespace FileLauncher.App;

/// <summary>
/// 常駐モード（SPEC §3.2 / §3.4）。盤面を出しっぱなしにし、前回の位置に出す。閉じる条件（外クリック・起動後など）は使わない。
/// トリガーは前面化（オプションでカーソル位置へ移動）、Esc は直前のアプリへフォーカスを返すだけ。
/// 重ね順は IWindowService.SetZOrder、「自動で隠す」は画面端に貼り付いているときだけ、離れて 500 ms 後に帯 4 px を残して収納する。
/// </summary>
internal sealed class ResidentController : IBoardController
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly long DebounceTicks = Stopwatch.Frequency / 4;

    private readonly BoardWindow _window;
    private readonly IPlatformServices _platform;
    private readonly SettingsHub _hub;
    private readonly DispatcherTimer _saveTimer = new() { Interval = SaveDelay };
    private readonly DispatcherTimer _autoHideTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private bool _active;
    private bool _modal;
    private nint _previousForeground;
    private long _lastTriggerTicks;

    // 自動で隠す: 収納中か、収納前（表示中）の位置、盤面の外に出た時刻、プログラムからの移動中か
    private bool _collapsed;
    private PixelPoint _expandedPosition;
    private long _leftAt;
    private bool _moving;
    private CancellationTokenSource? _moveCts;
    private bool _raisedFromBottom;

    public ResidentController(BoardWindow window, IPlatformServices platform, SettingsHub hub)
    {
        _window = window;
        _platform = platform;
        _hub = hub;
    }

    private AppSettings Settings => _hub.Current;

    public bool IsShown => _active && _window.IsVisible;

    public void Attach()
    {
        _platform.Hotkey.Pressed += ts => Dispatcher.UIThread.Post(() => { if (_active) OnTrigger(null, ts); });
        _platform.MouseTrigger.Triggered += (p, ts) => Dispatcher.UIThread.Post(() => { if (_active) OnTrigger(p, ts); });
        _platform.MouseTrigger.WheelTriggered += (p, ts, delta) => Dispatcher.UIThread.Post(() =>
        {
            if (!_active) return;
            OnTrigger(p, ts);
            _window.SwitchPageBy(delta);
        });
        _window.EscapePressed += () => { if (_active) ReturnFocus("Esc"); };
        _window.ItemInvoked += (item, newWindow) => { if (_active) Launch(item, null, newWindow); };
        _window.PositionChanged += (_, _) => { if (_active && !_moving && !_collapsed) ScheduleSave(); };
        _window.Deactivated += (_, _) => { if (_active) OnBoardDeactivated(); };
        _saveTimer.Tick += (_, _) => SaveBounds();
        _autoHideTimer.Tick += (_, _) => OnAutoHideTick();
    }

    // ---------------- 有効 / 無効 ----------------

    public void Activate()
    {
        _active = true;
        _collapsed = false;
        _window.Topmost = false; // 重ね順は SetZOrder で（SPEC §3.2）
        _window.AllowMove = !Settings.Resident.LockPosition;

        // 盤面の大きさを確定させてから位置を決める（ポップアップの準備運動などで大きさが崩れていると、
        // 画面内に寄せる計算で左上に貼り付いてしまう。2026-10-03 Mac で発生）
        _window.RestoreSize();
        var (w, h) = SizeInScreenUnits();
        if (w <= 0 || h <= 0 || w > 20000 || h > 20000) (w, h) = (400, 300);
        var screens = _window.Screens;
        ScreenRect WorkAreaAt(ScreenPoint p)
        {
            var s = screens.ScreenFromPoint(new PixelPoint(p.X, p.Y)) ?? screens.Primary;
            return s is null ? new ScreenRect(0, 0, 1920, 1080) : ToRect(s.WorkingArea);
        }
        var primary = screens.Primary is { } ps ? ToRect(ps.WorkingArea) : new ScreenRect(0, 0, 1920, 1080);
        var b = Settings.Resident.Bounds;
        var pos = ResidentLayout.InitialPosition(b?.X, b?.Y, w, h, WorkAreaAt, primary);
        AppLog.Info($"常駐: 保存位置={(b is null ? "なし" : $"{b.X},{b.Y}")} 大きさ={w}x{h} (Width={_window.Width}, Scaling={Scaling}) 主画面={primary}");
        _window.Position = new PixelPoint(pos.X, pos.Y);
        _expandedPosition = _window.Position;

        _window.PrepareShow();
        _window.ShowActivated = false; // 常駐の表示ではフォーカスを奪わない
        _window.Show();
        _window.ShowActivated = true;
        if (_window.TryGetPlatformHandle()?.Handle is { } handle)
        {
            _platform.Window.ConfigureBoardWindow(handle, DisplayMode.Resident);
            _platform.Window.SetZOrder(handle, Settings.Resident.ZOrder);
        }
        _ = _window.PlayShowAsync(CancellationToken.None);
        UpdateAutoHide();
        AppLog.Info($"常駐: 表示 at {pos.X},{pos.Y} 重ね順={Settings.Resident.ZOrder}");
    }

    public async Task DeactivateAsync()
    {
        if (!_active) return;
        _window.CancelDrag();
        _window.ClearSelection();
        _autoHideTimer.Stop();
        _moveCts?.Cancel();
        SaveBounds();
        _active = false; // ここから先の位置の通知・トリガーは無視（終了時の片付けで位置が 0,0 になっても保存しない）
        if (_window.IsVisible && !_collapsed)
        {
            // ポップアップへ戻す・終了するとき: 非表示の演出（サイバーパンクならグリッチ + フェードアウト）
            _window.IsHitTestVisible = false;
            try { await _window.PlayHideAsync(CancellationToken.None); }
            finally { _window.IsHitTestVisible = true; }
        }
        if (_collapsed) _window.Position = _expandedPosition;
        _collapsed = false;
        _window.AmbientSuspended = false;
        _window.Hide();
    }

    /// <summary>常駐タブの設定（重ね順・自動で隠す・位置をロック）を反映する。</summary>
    public void ApplySettings()
    {
        if (!_active) return;
        _window.AllowMove = !Settings.Resident.LockPosition;
        if (_window.TryGetPlatformHandle()?.Handle is { } handle) _platform.Window.SetZOrder(handle, Settings.Resident.ZOrder);
        UpdateAutoHide();
    }

    // ---------------- トリガー ----------------

    private void OnTrigger(ScreenPoint? point, long timestamp)
    {
        if (timestamp - _lastTriggerTicks < DebounceTicks) return;
        _lastTriggerTicks = timestamp;
        BringToFront("trigger", point ?? _platform.Window.GetCursorPosition());
    }

    public void Toggle(string source) => BringToFront(source, null);

    public void ShowFromExternal(string source) => BringToFront(source, null);

    /// <summary>前面化（常駐では閉じない。SPEC §3.4）。オプションでカーソル位置へ移動、収納中なら出す。</summary>
    private void BringToFront(string source, ScreenPoint? cursor)
    {
        if (!_active) return;
        if (!_window.IsVisible) _window.Show();
        _previousForeground = _platform.Window.CaptureForeground();
        if (_collapsed) Expand();

        if (cursor is { } c && Settings.Resident.MoveToCursorOnTrigger)
        {
            // カーソル位置へ（popup の表示位置の組は見ない。常駐はアンカー設定の対象外で常に上中央。SPEC §3.3）
            var (w, h) = SizeInScreenUnits();
            var screens = _window.Screens;
            var s = screens.ScreenFromPoint(new PixelPoint(c.X, c.Y)) ?? screens.Primary;
            var wa = s is null ? new ScreenRect(0, 0, 1920, 1080) : ToRect(s.WorkingArea);
            var target = PopupPlacement.ClampInto(PopupPlacement.CursorOrigin(c, w, h, CursorAnchor.Top, PopupPlacement.InsetFor(Scaling)), w, h, wa);
            MoveTo(new PixelPoint(target.X, target.Y), EffectCatalog.Resolve(Settings.Appearance, EffectCatalog.ResidentMove), saveAfter: true);
        }

        if (_window.TryGetPlatformHandle()?.Handle is { } handle)
        {
            if (Settings.Resident.ZOrder == ZOrder.Bottommost)
            {
                // 最背面のままでは前に出ないので一時的に通常へ。非アクティブになったら最背面へ戻す（SPEC §3.2）
                _platform.Window.SetZOrder(handle, ZOrder.Normal);
                _raisedFromBottom = true;
            }
            _platform.Window.BringToForeground(handle);
        }
        _window.Activate();
        _window.Focus();
        // 前面化の合図に一瞬グリッチ（フェードはしない: 見えている盤面を一度消さない）
        if (!_moving) _ = _window.PlayGlitchAsync(appearing: true, fade: false, CancellationToken.None);
        AppLog.Info($"常駐: 前面化 ({source})");
    }

    private void OnBoardDeactivated()
    {
        if (_raisedFromBottom && _window.TryGetPlatformHandle()?.Handle is { } handle)
        {
            _platform.Window.SetZOrder(handle, ZOrder.Bottommost);
            _raisedFromBottom = false;
        }
    }

    private void ReturnFocus(string reason)
    {
        bool ok = _previousForeground != 0 && _platform.Window.RestoreForeground(_previousForeground);
        AppLog.Info($"常駐: {reason} → focus {(ok ? "returned" : "not returned")}");
        _previousForeground = 0;
    }

    // ---------------- 起動・ダイアログ ----------------

    public void Launch(LauncherItem item, IReadOnlyList<string>? droppedPaths, bool newWindow = false)
    {
        var folderTarget = FolderOpening.Resolve(Settings.General.FolderOpenTarget, newWindow);
        var result = _platform.Shell.Launch(item, droppedPaths, folderTarget);
        AppLog.Info($"launch '{item.Name}' (resident){(item.Kind == ItemKind.Folder ? $" folder={folderTarget}" : "")} → {(result.Success ? "ok" : result.Error)}");
        if (!result.Success)
        {
            Toast.Show(Strings.FormatToast_LaunchFailed(item.Name, Loc.LaunchError(result)));
            return;
        }
        _window.PlayLaunch(item); // 常駐では閉じない（SPEC §3.2）
    }

    public async Task<T> RunModalAsync<T>(Func<Task<T>> dialog)
    {
        _modal = true;
        try { return await dialog(); }
        finally { _modal = false; }
    }

    // ---------------- 位置の保存 ----------------

    private void ScheduleSave()
    {
        _expandedPosition = _window.Position;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveBounds()
    {
        _saveTimer.Stop();
        if (!_active) return;
        // ウィンドウから今読まず、動くたびに覚えた位置を使う（終了時の macOS はウィンドウが片付けられていて
        // Position が 0,0 を返し、それを保存して次回左上に出ていた。2026-10-03）
        var p = _expandedPosition;
        var (w, h) = SizeInScreenUnits();
        if (w <= 0 || h <= 0) return;
        var bounds = new WindowBounds(p.X, p.Y, w, h);
        if (Settings.Resident.Bounds == bounds) return;
        _hub.Update(s => s.Resident.Bounds = bounds, SettingsChange.None);
        AppLog.Info($"常駐: 位置を保存 {bounds.X},{bounds.Y}");
    }

    // ---------------- 自動で隠す（SPEC §3.2） ----------------

    private void UpdateAutoHide()
    {
        if (_active && Settings.Resident.AutoHide) _autoHideTimer.Start();
        else
        {
            _autoHideTimer.Stop();
            if (_collapsed) Expand();
        }
    }

    private void OnAutoHideTick()
    {
        if (!_active || _moving || _modal || _window.IsDragging) return; // 盤面内のドラッグ中は隠さない
        var cursor = _platform.Window.GetCursorPosition();
        var rect = WindowRect();
        bool inside = rect.Contains(cursor);

        if (_collapsed)
        {
            if (inside) Expand(); // 帯に触れたら出す
            return;
        }

        var wa = WorkAreaOf(rect);
        var edge = ResidentLayout.StuckEdge(rect, wa);
        if (edge == ScreenEdge.None || inside) // ドラッグで動かしている間はカーソルが盤面の上にあるので収納しない
        {
            if (_leftAt != 0 && edge == ScreenEdge.None) AppLog.Info($"常駐: 自動で隠さない（端に貼り付いていない: 盤面={rect} 作業領域={wa}）");
            _leftAt = 0;
            return;
        }
        if (_leftAt == 0)
        {
            _leftAt = Stopwatch.GetTimestamp();
            return;
        }
        if (Stopwatch.GetElapsedTime(_leftAt) < CollapseDelay) return;

        _leftAt = 0;
        _expandedPosition = _window.Position;
        var target = ResidentLayout.CollapsedPosition(rect, edge, wa);
        _collapsed = true;
        _window.AmbientSuspended = true; // 収納中は光の玉・明滅を止める
        // 端へ隠れる: スライドしながらグリッチ（フェードはしない: 隠れたあとも 4 px の帯を見せておくため）
        _ = _window.PlayGlitchAsync(appearing: false, fade: false, CancellationToken.None);
        MoveTo(new PixelPoint(target.X, target.Y), EffectCatalog.Resolve(Settings.Appearance, EffectCatalog.ResidentAutoHide), saveAfter: false);
        AppLog.Info($"常駐: 自動で隠す（{edge}）");
    }

    private void Expand()
    {
        AppLog.Info("常駐: 収納から戻す");
        _collapsed = false;
        _window.AmbientSuspended = false; // グリッチ中は BoardWindow 側で止まっていて、終わったら再開する
        // 端から出てくる: スライドしながらグリッチ + フェードイン
        _ = _window.PlayGlitchAsync(appearing: true, fade: true, CancellationToken.None);
        MoveTo(_expandedPosition, EffectCatalog.Resolve(Settings.Appearance, EffectCatalog.ResidentAutoHide), saveAfter: false);
    }

    private async void MoveTo(PixelPoint target, EffectSpec spec, bool saveAfter)
    {
        _moveCts?.Cancel();
        var cts = _moveCts = new CancellationTokenSource();
        _moving = true;
        try { await WindowMover.MoveAsync(_window, target, spec, cts.Token); }
        finally
        {
            if (_moveCts == cts) _moving = false;
        }
        if (saveAfter && !cts.IsCancellationRequested)
        {
            _expandedPosition = _window.Position;
            SaveBounds();
        }
    }

    // ---------------- 座標 ----------------

    private double Scaling => _platform.Window.UsesLogicalScreenCoordinates ? 1.0 : _window.RenderScaling;

    private (int W, int H) SizeInScreenUnits() =>
        ((int)Math.Ceiling(_window.Width * Scaling), (int)Math.Ceiling(_window.Height * Scaling));

    private ScreenRect WindowRect()
    {
        var (w, h) = SizeInScreenUnits();
        return new ScreenRect(_window.Position.X, _window.Position.Y, w, h);
    }

    private ScreenRect WorkAreaOf(ScreenRect rect)
    {
        var center = new PixelPoint(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        var s = _window.Screens.ScreenFromPoint(center) ?? _window.Screens.Primary;
        return s is null ? new ScreenRect(0, 0, 1920, 1080) : ToRect(s.WorkingArea);
    }

    private static ScreenRect ToRect(PixelRect r) => new(r.X, r.Y, r.Width, r.Height);
}
