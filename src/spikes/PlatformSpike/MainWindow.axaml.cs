using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PlatformSpike.Native;

namespace PlatformSpike;

public partial class MainWindow : Window
{
    private readonly PopupWindow _popup = new();
    private readonly InputHookService _hook = new();
    private IntPtr _previousForeground = IntPtr.Zero;
    private DateTime _lastTriggerAt = DateTime.MinValue;
    private System.Diagnostics.Stopwatch? _showTimer;

    public MainWindow()
    {
        InitializeComponent();

        _popup.Log += Log;
        _popup.ItemLaunched += () => HidePopup("item launched");
        _popup.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && e.NewValue is false)
                OnPopupHidden();
            if (e.Property == IsVisibleProperty && e.NewValue is true && _showTimer is not null)
            {
                Log($"show latency ≈ {_showTimer.ElapsedMilliseconds} ms (trigger → visible)");
                _showTimer = null;
            }
        };

        _hook.Log += Log;
        _hook.Triggered += OnTriggered;
        _hook.MousePassedThrough += OnMousePassedThrough;
        _hook.MouseReleasedPassedThrough += OnMouseReleasedPassedThrough;

        ShowAtCursorButton.Click += (_, _) => ShowPopupAtCursor(null);
        ShowCenterButton.Click += (_, _) => { CapturePrev(); _popup.ShowCentered(); };
        HideButton.Click += (_, _) => HidePopup("button");
        TopmostCheck.IsCheckedChanged += (_, _) => _popup.Topmost = TopmostCheck.IsChecked == true;
        CloseOnDeactivateCheck.IsCheckedChanged += (_, _) => _popup.CloseOnDeactivate = CloseOnDeactivateCheck.IsChecked == true;
        SuppressHotkeyCheck.IsCheckedChanged += (_, _) => _hook.SuppressHotkey = SuppressHotkeyCheck.IsChecked == true;

        ClearLogButton.Click += (_, _) => LogBox.Text = string.Empty;
        CopyLogButton.Click += async (_, _) =>
        {
            if (Clipboard is { } cb) await cb.SetTextAsync(LogBox.Text ?? string.Empty);
        };
        QuitButton.Click += (_, _) =>
        {
            _hook.Dispose();
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        };

        Opened += async (_, _) =>
        {
            Log($"OS={RuntimeInformation.OSDescription} arch={RuntimeInformation.ProcessArchitecture} .NET={Environment.Version}");
            Log($"screens: " + string.Join(" | ", Screens.All.Select(s => $"{s.Bounds} wa={s.WorkingArea} scale={s.Scaling}")));
            await _hook.StartAsync();
        };
    }

    // --- トリガー（フックスレッドから呼ばれる） ---
    private void OnTriggered(TriggerKind kind, short x, short y)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 連続発火の抑制（左右同時は左右それぞれで発火しうる）
            var now = DateTime.UtcNow;
            if ((now - _lastTriggerAt).TotalMilliseconds < 250) return;
            _lastTriggerAt = now;

            Log($"TRIGGER {kind} ({x},{y})");

            if (_popup.IsVisible)
            {
                HidePopup("toggle");
                return;
            }

            _showTimer = System.Diagnostics.Stopwatch.StartNew();
            ShowPopupAtCursor(x >= 0 ? new PixelPoint(x, y) : null);
        });
    }

    // --- 外クリックで閉じる（フックスレッドから呼ばれる） ---
    // Deactivated は、フック経由で前面化したポップアップでは外クリックしても来ないことがある（Win で確認）。
    // そのためフックのマウス押下座標をポップアップ矩形と比較して閉じる。macOS でも同じ方式で済む。
    //
    // 押下時点では閉じず、離上時に判定する（Explorer / Finder からつかんで盤面へ D&D 登録できるように）:
    //   - ほぼ動かさずに離した → 外クリックとして閉じる
    //   - ドラッグして盤面上で離した → ドロップなので開いたまま
    //   - ドラッグして盤面外で離した → 閉じる
    private const int DragThresholdPx = 4; // Windows の SM_CXDRAG 既定値相当
    private PixelPoint? _outsidePressAt;

    private void OnMousePassedThrough(short x, short y)
    {
        // 非アクティブ化による自動クローズを押下中は止める。UI スレッドに Post する前に立てる
        // （つかんだ瞬間に Explorer がアクティブになり Deactivated が先に届くため）
        _popup.HoldOpenWhilePressed = true;

        Dispatcher.UIThread.Post(() =>
        {
            if (!_popup.IsVisible || !_popup.CloseOnDeactivate) return;
            var p = new PixelPoint(x, y);
            if (_popup.ScreenBounds.Contains(p)) return;
            _outsidePressAt = p;
        });
    }

    private void OnMouseReleasedPassedThrough(short x, short y)
    {
        _popup.HoldOpenWhilePressed = false;

        Dispatcher.UIThread.Post(() =>
        {
            if (_outsidePressAt is not { } start) return;
            _outsidePressAt = null;
            if (!_popup.IsVisible) return;

            var end = new PixelPoint(x, y);
            bool dragged = Math.Abs(end.X - start.X) > DragThresholdPx || Math.Abs(end.Y - start.Y) > DragThresholdPx;
            if (dragged && _popup.ScreenBounds.Contains(end))
            {
                Log($"drag from outside dropped on popup ({x},{y}) → keep open");
                return;
            }

            _popup.FocusMovedElsewhere = true; // クリック先のアプリにフォーカスが移るので戻さない
            HidePopup(dragged ? $"drag ended outside ({x},{y})" : $"outside click ({x},{y})");
        });
    }

    private void ShowPopupAtCursor(PixelPoint? hookPoint)
    {
        CapturePrev();

        // SharpHook の座標は Windows では物理 px、macOS では pt の想定。
        // Avalonia の PixelPoint と単位が一致するかをログで照合する（検証ポイント）。
        PixelPoint p;
        if (hookPoint is { } hp)
        {
            p = hp;
            if (OperatingSystem.IsWindows())
                Log($"window under cursor: {WindowsNative.ClassNameUnderPoint(hp.X, hp.Y)}");
            if (OperatingSystem.IsMacOS())
                foreach (var w in MacWindowList.WindowsAt(hp.X, hp.Y))
                    Log($"window under cursor: {w}");
        }
        else
        {
            // 手動ボタンから呼ばれた場合はこのウィンドウの位置の近くに出す
            p = Position + new PixelPoint(100, 100);
        }

        _popup.CloseOnDeactivate = CloseOnDeactivateCheck.IsChecked == true;
        _popup.Topmost = TopmostCheck.IsChecked == true;
        _popup.ShowAt(p);
    }

    private int _previousPid;

    private void CapturePrev()
    {
        if (OperatingSystem.IsMacOS())
        {
            _previousPid = MacNative.FrontmostPid();
            Log($"captured frontmost app pid={_previousPid} ({MacNative.FrontmostName()})");
        }
        if (OperatingSystem.IsWindows())
        {
            _previousForeground = WindowsNative.CaptureForeground();
            Log($"captured foreground hwnd=0x{_previousForeground:X}");
        }
    }

    private void HidePopup(string reason)
    {
        Log($"hide popup ({reason})");
        if (_popup.IsVisible) _popup.Hide();
        else OnPopupHidden();
    }

    private void OnPopupHidden()
    {
        if (_popup.FocusMovedElsewhere)
        {
            // 外クリック・非アクティブ化で閉じた場合、フォーカスはクリック先が持つべきなので戻さない
            Log("restore foreground → skipped (focus moved elsewhere)");
            _previousForeground = IntPtr.Zero;
        }
        else if (OperatingSystem.IsWindows() && RestoreFocusCheck.IsChecked == true && _previousForeground != IntPtr.Zero)
        {
            bool ok = WindowsNative.RestoreForeground(_previousForeground);
            Log($"restore foreground → {(ok ? "ok" : "FAILED")}");
            _previousForeground = IntPtr.Zero;
        }
        else if (OperatingSystem.IsMacOS() && RestoreFocusCheck.IsChecked == true && _previousPid != 0)
        {
            if (MacHideAppCheck.IsChecked == true)
            {
                MacNative.HideApp();
                Log("restore foreground (mac B: NSApp hide)");
            }
            else
            {
                bool ok = MacNative.ActivatePid(_previousPid);
                Log($"restore foreground (mac A: activate pid={_previousPid}) → {(ok ? "ok" : "FAILED")}");
            }
            _previousPid = 0;
            DispatcherTimer.RunOnce(() => Log($"frontmost after restore: {MacNative.FrontmostName()}"), TimeSpan.FromMilliseconds(300));
        }
    }

    private void Log(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Log(message));
            return;
        }
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        LogBox.Text += $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
        LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
        StatusText.Text = message.Length > 60 ? message[..60] + "…" : message;
    }
}
