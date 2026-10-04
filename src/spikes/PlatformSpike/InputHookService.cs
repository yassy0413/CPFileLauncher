using System.Diagnostics;
using SharpHook;
using SharpHook.Data;

namespace PlatformSpike;

public enum TriggerKind
{
    Hotkey,            // Ctrl+Alt+Space
    CtrlMiddleClick,   // Ctrl(⌃) + 中クリック（マウストリガー既定）
    AltMiddleClick,    // Alt(⌥) + 中ボタン
    MiddleLongPress,   // 中ボタン長押し
    LeftRightTogether, // 左右同時クリック
}

/// <summary>
/// SharpHook (libuiohook) でグローバル入力を監視し、ランチャー呼び出しトリガーを検出する。
/// 検証対象:
///   - Win / Mac 両方でフックが開始できるか（Mac は権限が必要）
///   - 各トリガーが誤検出なく拾えるか
///   - SuppressEvent でホットキーを他アプリに渡さずに済むか
///   - イベント座標の単位（Win: 物理px / Mac: pt）
/// </summary>
public sealed class InputHookService : IDisposable
{
    // 注意: イベント抑止 (SuppressEvent) は SimpleGlobalHook でのみ有効。
    // TaskPoolGlobalHook は非同期ディスパッチのため抑止できない。
    private readonly SimpleGlobalHook _hook = new();

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // 「まだ押されていない」の番兵値。long.MinValue だと now - x がオーバーフローして負になり、
    // 単発クリックが同時押し判定されてしまうので、引き算しても溢れない値にする。
    private const long NeverPressed = long.MinValue / 2;
    private long _lastLeftDownMs = NeverPressed;
    private long _lastRightDownMs = NeverPressed;
    private CancellationTokenSource? _middleHold;
    // ホットキー押下中フラグ。キーリピートで KeyPressed が連続するため、離すまで再発火しない。
    private bool _hotkeyHeld;
    private int _hotkeyRepeatCount;
    // 中ボタンの押下をトリガーとして抑止したか（対になる離上も抑止するため）
    private bool _middleSuppressed;

    public int LongPressMs { get; set; } = 400;
    public int SimultaneousWindowMs { get; set; } = 100;
    public bool SuppressHotkey { get; set; } = true;

    /// <summary>ログ出力（フックスレッドから呼ばれる）。</summary>
    public event Action<string>? Log;

    /// <summary>トリガー検出（フックスレッドから呼ばれる。UI 操作は Dispatcher 経由で）。</summary>
    public event Action<TriggerKind, short, short>? Triggered;

    /// <summary>トリガーにならず下のアプリに通したマウス押下（フックスレッドから呼ばれる）。ポップアップの外クリック判定用。</summary>
    public event Action<short, short>? MousePassedThrough;

    /// <summary>下のアプリに通したマウス離上（フックスレッドから呼ばれる）。外クリックかドラッグかの判定用。</summary>
    public event Action<short, short>? MouseReleasedPassedThrough;

    public InputHookService()
    {
        _hook.HookEnabled += (_, _) => Log?.Invoke("hook enabled");
        _hook.HookDisabled += (_, _) => Log?.Invoke("hook disabled");
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MousePressed += OnMousePressed;
        _hook.MouseReleased += OnMouseReleased;
    }

    public async Task StartAsync()
    {
        try
        {
            Log?.Invoke("starting hook...");
            await _hook.RunAsync();
            Log?.Invoke("hook stopped");
        }
        catch (HookException ex)
        {
            // macOS: 「入力監視」「アクセシビリティ」未許可だとここに来る想定。
            //        Result の値を記録して、どちらの権限が必要かを特定する。
            Log?.Invoke($"HOOK FAILED: {ex.Result} - {ex.Message}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"HOOK FAILED (unexpected): {ex}");
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var mask = e.RawEvent.Mask;
        bool ctrl = (mask & EventMask.Ctrl) != 0;
        bool alt = (mask & EventMask.Alt) != 0;
        bool shift = (mask & EventMask.Shift) != 0;
        bool meta = (mask & EventMask.Meta) != 0;

        if (e.Data.KeyCode == KeyCode.VcSpace && ctrl && alt && !shift && !meta)
        {
            if (SuppressHotkey)
            {
                e.SuppressEvent = true; // 他アプリにスペースを渡さない（リピート分も含む）
            }
            if (_hotkeyHeld)
            {
                _hotkeyRepeatCount++; // キーリピート → 無視
                return;
            }
            _hotkeyHeld = true;
            _hotkeyRepeatCount = 0;
            Log?.Invoke($"HOTKEY Ctrl+Alt+Space (mask={mask}, suppressed={SuppressHotkey})");
            Triggered?.Invoke(TriggerKind.Hotkey, -1, -1);
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        // 修飾キーを先に離しても Space の離上で解除する
        if (e.Data.KeyCode == KeyCode.VcSpace && _hotkeyHeld)
        {
            _hotkeyHeld = false;
            if (SuppressHotkey)
            {
                e.SuppressEvent = true; // 押下を抑止したので離上も渡さない
            }
            if (_hotkeyRepeatCount > 0)
            {
                Log?.Invoke($"hotkey released (ignored {_hotkeyRepeatCount} key repeats)");
            }
        }
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        var d = e.Data;
        var mask = e.RawEvent.Mask;
        bool alt = (mask & EventMask.Alt) != 0;
        bool anyMod = (mask & (EventMask.Ctrl | EventMask.Alt | EventMask.Shift | EventMask.Meta)) != 0;
        bool ctrlOnly = (mask & EventMask.Ctrl) != 0
                        && (mask & (EventMask.Alt | EventMask.Shift | EventMask.Meta)) == 0;
        long now = _clock.ElapsedMilliseconds;

        Log?.Invoke($"mouse down {d.Button} at ({d.X},{d.Y}) mask={mask} clicks={d.Clicks} type={e.RawEvent.Type}");

        switch (d.Button)
        {
            case MouseButton.Button3: // 中ボタン
                if (ctrlOnly)
                {
                    // Ctrl+中クリック（既定）: ブラウザの「リンクをバックグラウンドの新規タブで開く」等を下のアプリに渡さない
                    e.SuppressEvent = true;
                    _middleSuppressed = true;
                    Triggered?.Invoke(TriggerKind.CtrlMiddleClick, d.X, d.Y);
                }
                else if (alt)
                {
                    e.SuppressEvent = true;
                    _middleSuppressed = true;
                    Triggered?.Invoke(TriggerKind.AltMiddleClick, d.X, d.Y);
                }
                else if (!anyMod)
                {
                    // 長押し判定: 押下時点では抑止できない（短押しをアプリに通す必要があるため）。
                    // → 長押し成立時、既に中クリックは下のアプリに届いている。この挙動を許容できるかが検証ポイント。
                    _middleHold?.Cancel();
                    var cts = new CancellationTokenSource();
                    _middleHold = cts;
                    short x = d.X, y = d.Y;
                    _ = Task.Delay(LongPressMs, cts.Token).ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            Log?.Invoke("middle long press");
                            Triggered?.Invoke(TriggerKind.MiddleLongPress, x, y);
                        }
                    }, TaskScheduler.Default);
                }
                break;

            case MouseButton.Button1:
                _lastLeftDownMs = now;
                if (now - _lastRightDownMs <= SimultaneousWindowMs)
                {
                    _lastLeftDownMs = _lastRightDownMs = NeverPressed; // 1 回の同時押しで 1 回だけ発火
                    Triggered?.Invoke(TriggerKind.LeftRightTogether, d.X, d.Y);
                }
                break;

            case MouseButton.Button2:
                _lastRightDownMs = now;
                if (now - _lastLeftDownMs <= SimultaneousWindowMs)
                {
                    _lastLeftDownMs = _lastRightDownMs = NeverPressed; // 1 回の同時押しで 1 回だけ発火
                    Triggered?.Invoke(TriggerKind.LeftRightTogether, d.X, d.Y);
                }
                break;
        }

        if (!e.SuppressEvent)
        {
            MousePassedThrough?.Invoke(d.X, d.Y);
        }
    }

    private void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        Log?.Invoke($"mouse up {e.Data.Button} at ({e.Data.X},{e.Data.Y}) mask={e.RawEvent.Mask} clicks={e.Data.Clicks}");
        if (e.Data.Button == MouseButton.Button3)
        {
            if (_middleSuppressed)
            {
                // 押下を抑止したので離上も渡さない（修飾キーを先に離していても対で抑止する）
                e.SuppressEvent = true;
                _middleSuppressed = false;
            }
            _middleHold?.Cancel();
            _middleHold = null;
        }
        if (!e.SuppressEvent)
        {
            MouseReleasedPassedThrough?.Invoke(e.Data.X, e.Data.Y);
        }
    }

    public void Dispose()
    {
        _middleHold?.Cancel();
        _hook.Dispose();
    }
}
