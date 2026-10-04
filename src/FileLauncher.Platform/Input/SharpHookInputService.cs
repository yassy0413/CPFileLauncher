using System.Diagnostics;
using FileLauncher.Core.Input;
using FileLauncher.Core.Model;
using SharpHook;
using SharpHook.Data;

namespace FileLauncher.Platform.Input;

/// <summary>
/// SharpHook（libuiohook）によるグローバル入力フック。ホットキーとマウストリガーで 1 つのフックを共有する。
/// Windows / macOS 共通（OS 差はライブラリが吸収）。判定ロジックは Core の TriggerDetector。
/// イベント抑止（SuppressEvent）が要るので SimpleGlobalHook を使う（TaskPoolGlobalHook は抑止できない）。
/// </summary>
public sealed class SharpHookInputService : IInputHookService, IHotkeyService, IMouseTriggerService
{
    private readonly object _gate = new();
    private SimpleGlobalHook? _hook;
    private readonly TriggerDetector _detector;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private volatile bool _running;

    private readonly Func<KeyModifiers>? _currentModifiers;

    /// <param name="currentModifiers">
    /// マウス押下時の修飾キーを OS から直接読む関数（任意）。macOS では自アプリが前面のときに押した ⌃ などを
    /// フックが修飾キーとして記録しないことがあるため（2026-10-03 確認）、Mac はこれを渡す。null ならフックの値を使う。
    /// </param>
    public SharpHookInputService(Func<ScreenPoint, bool>? isDesktop = null, Func<KeyModifiers>? currentModifiers = null)
    {
        _detector = new TriggerDetector(isDesktop);
        _currentModifiers = currentModifiers;
    }

    public bool IsRunning => _running;

    public event Action<string>? Failed;
    public event Action<long>? Pressed;
    public event Action<ScreenPoint, long>? Triggered;
    public event Action<ScreenPoint>? PassedThroughDown;
    public event Action<ScreenPoint, long, int>? WheelTriggered;
    public event Action<ScreenPoint>? PassedThroughUp;

    public void Start()
    {
        SimpleGlobalHook hook;
        lock (_gate)
        {
            if (_running) return;
            // 失敗・停止したフックは再利用できないので作り直す（SPEC §10.3。Mac で権限を許可した後の再試行）
            _hook?.Dispose();
            _hook = hook = CreateHook();
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await hook.RunAsync(useBackgroundThread: true);
            }
            catch (HookException ex)
            {
                // macOS: アクセシビリティ未許可だと ErrorAxApiDisabled でここに来る（SPEC §4.3）
                Failed?.Invoke($"{ex.Result}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex.ToString());
            }
        });
    }

    private SimpleGlobalHook CreateHook()
    {
        var hook = new SimpleGlobalHook();
        hook.HookEnabled += (_, _) => _running = true;
        hook.HookDisabled += (_, _) => _running = false;
        hook.KeyPressed += OnKeyPressed;
        hook.KeyReleased += OnKeyReleased;
        hook.MousePressed += OnMousePressed;
        hook.MouseReleased += OnMouseReleased;
        hook.MouseWheel += OnMouseWheel;
        return hook;
    }

    // ---------------- IHotkeyService ----------------

    public bool Register(HotkeySetting hotkey, out string? error)
    {
        // フック方式は登録という手続きが無く、他アプリとの競合も検知できない（SPEC §13.1-2）
        _detector.SetHotkey(hotkey);
        error = null;
        return true;
    }

    public void Unregister() => _detector.SetHotkey(null);

    private volatile Action<string, KeyModifiers>? _capture;
    private volatile string? _capturedKey; // 記録で握りつぶしたキー（離上も渡さない）

    public void BeginCapture(Action<string, KeyModifiers> onKey) => _capture = onKey;

    public void EndCapture() => _capture = null;

    // ---------------- IMouseTriggerService ----------------

    public void Configure(TriggerSettings settings) => _detector.SetMouse(settings);

    // ---------------- フックスレッド ----------------

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (IsModifierKey(e.Data.KeyCode)) return;
        if (_capture is { } capture)
        {
            e.SuppressEvent = true;
            _capturedKey = KeyName(e.Data.KeyCode);
            capture(_capturedKey, ToModifiers(e.RawEvent.Mask));
            return;
        }
        var d = _detector.OnKeyDown(KeyName(e.Data.KeyCode), ToModifiers(e.RawEvent.Mask));
        if (d.Suppress) e.SuppressEvent = true;
        if (d.Fire) Pressed?.Invoke(Stopwatch.GetTimestamp());
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (IsModifierKey(e.Data.KeyCode)) return;
        if (_capturedKey is { } captured && captured == KeyName(e.Data.KeyCode))
        {
            e.SuppressEvent = true;
            _capturedKey = null;
            return;
        }
        if (_detector.OnKeyUp(KeyName(e.Data.KeyCode))) e.SuppressEvent = true;
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        long ts = Stopwatch.GetTimestamp();
        var p = new ScreenPoint(e.Data.X, e.Data.Y);
        var mods = _currentModifiers?.Invoke() ?? ToModifiers(e.RawEvent.Mask);
        var d = _detector.OnMouseDown(ToButton(e.Data.Button), mods, p, _clock.ElapsedMilliseconds);
        DebugLog($"mouse down {e.Data.Button} mods={mods} hookMask={e.RawEvent.Mask} → {d}");
        if (d.Suppress) e.SuppressEvent = true;
        if (d.Fire) Triggered?.Invoke(p, ts);
        if (d.PassedThrough) PassedThroughDown?.Invoke(p);
        if (d.LongPressToken is { } token) StartLongPress(token);
    }

    private void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        var d = _detector.OnMouseUp(ToButton(e.Data.Button));
        DebugLog($"mouse up {e.Data.Button} → {d}");
        if (d.Suppress) e.SuppressEvent = true;
        if (d.PassedThrough) PassedThroughUp?.Invoke(new ScreenPoint(e.Data.X, e.Data.Y));
    }

    private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        if (e.Data.Direction != MouseWheelScrollDirection.Vertical) return;
        var p = new ScreenPoint(e.Data.X, e.Data.Y);
        var mods = _currentModifiers?.Invoke() ?? ToModifiers(e.RawEvent.Mask);
        var d = _detector.OnMouseWheel(e.Data.Rotation, mods, p);
        if (d.Suppress) e.SuppressEvent = true;
        if (d.PageDelta != 0) WheelTriggered?.Invoke(p, Stopwatch.GetTimestamp(), d.PageDelta);
    }

    private void StartLongPress(int token)
    {
        _ = Task.Delay(_detector.LongPressMs).ContinueWith(_ =>
        {
            bool ok = _detector.OnLongPressElapsed(token, out var p);
            DebugLog($"long press elapsed token={token} → {ok}");
            if (ok) Triggered?.Invoke(p, Stopwatch.GetTimestamp());
        }, TaskScheduler.Default);
    }

    /// <summary>Debug ビルドだけ、入力の判定を Trace に出す（App の AppLog がファイルへ転送する）。</summary>
    [Conditional("DEBUG")]
    private static void DebugLog(string message) => Trace.WriteLine("[hook] " + message);

    // ---------------- 変換 ----------------

    private static bool IsModifierKey(KeyCode k) => k is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftShift or KeyCode.VcRightShift or
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    /// <summary>KeyCode.VcSpace → "Space"、VcA → "A"、VcF1 → "F1"（設定の HotkeySetting.Key と同じ表記）。</summary>
    internal static string KeyName(KeyCode k)
    {
        var s = k.ToString();
        return s.StartsWith("Vc", StringComparison.Ordinal) ? s[2..] : s;
    }

    private static KeyModifiers ToModifiers(EventMask m)
    {
        var r = KeyModifiers.None;
        if ((m & EventMask.Ctrl) != 0) r |= KeyModifiers.Ctrl;
        if ((m & EventMask.Alt) != 0) r |= KeyModifiers.Alt;
        if ((m & EventMask.Shift) != 0) r |= KeyModifiers.Shift;
        if ((m & EventMask.Meta) != 0) r |= KeyModifiers.Meta;
        return r;
    }

    private static InputButton ToButton(MouseButton b) => b switch
    {
        MouseButton.Button1 => InputButton.Left,
        MouseButton.Button2 => InputButton.Right,
        MouseButton.Button3 => InputButton.Middle,
        MouseButton.Button4 => InputButton.X1,
        MouseButton.Button5 => InputButton.X2,
        _ => InputButton.Other,
    };

    public void Dispose()
    {
        lock (_gate) _hook?.Dispose();
    }
}
