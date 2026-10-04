using FileLauncher.Core.Model;

namespace FileLauncher.Core.Input;

public enum InputButton
{
    Left,
    Right,
    Middle,
    X1,
    X2,
    Other,
}

/// <param name="Suppress">このイベントを下のアプリに渡さない。</param>
/// <param name="Fire">トリガー成立。</param>
public readonly record struct KeyDecision(bool Suppress, bool Fire);

/// <param name="Suppress">このイベントを下のアプリに渡さない。</param>
/// <param name="Fire">トリガー成立。</param>
/// <param name="PassedThrough">トリガーと無関係に下のアプリへ通した（盤面外クリック判定に使う）。</param>
/// <param name="LongPressToken">非 null なら長押し判定を開始する。LongPressMs 後に OnLongPressElapsed(token) を呼ぶ。</param>
public readonly record struct MouseDecision(bool Suppress, bool Fire, bool PassedThrough, int? LongPressToken = null);

/// <param name="Suppress">このホイール回転を下のアプリに渡さない。</param>
/// <param name="PageDelta">0 以外なら「ホイールクリック + 回転」成立。+1 = 次のページ、-1 = 前のページ。</param>
public readonly record struct WheelDecision(bool Suppress, int PageDelta);

/// <summary>
/// グローバル入力からランチャー呼び出しトリガーを判定する（SPEC §4.1, §4.2）。入力フック実装（SharpHook 等）から切り離した純粋ロジック。
/// スパイク（src/spikes/PlatformSpike）で分かった落とし穴を反映:
///   - キーリピートで連続発火しない（キーを離すまで再発火しない。リピート分も抑止は続ける）
///   - トリガーにした押下を抑止したら、対になる離上も抑止する（修飾キーを先に離しても）
///   - 「まだ押していない」時刻の番兵値は引き算で溢れない値にする
/// スレッドセーフ（フックスレッドと長押しタイマーから呼ばれる）。
/// </summary>
public sealed class TriggerDetector
{
    private const long NeverPressed = long.MinValue / 2;

    private readonly object _lock = new();
    private readonly Func<ScreenPoint, bool>? _isDesktop;

    private HotkeySetting? _hotkey;
    private TriggerSettings _mouse = new() { Mouse = [] };

    private string? _heldHotkeyKey;
    private readonly HashSet<InputButton> _suppressedDown = new();
    private long _lastLeftDownMs = NeverPressed;
    private long _lastRightDownMs = NeverPressed;
    private int _longPressToken;
    private InputButton? _longPressButton;
    private ScreenPoint _longPressPoint;
    private bool _middleHeld; // 中ボタンを押している間（ホイールクリック + 回転、SPEC §4.2）

    /// <param name="isDesktop">「デスクトップ上のみ」用。カーソル下がデスクトップか。null なら常に true 扱い。</param>
    public TriggerDetector(Func<ScreenPoint, bool>? isDesktop = null) => _isDesktop = isDesktop;

    public void SetHotkey(HotkeySetting? hotkey)
    {
        lock (_lock)
        {
            _hotkey = hotkey is { Enabled: true } ? hotkey : null;
            _heldHotkeyKey = null;
        }
    }

    public void SetMouse(TriggerSettings settings)
    {
        lock (_lock)
        {
            _mouse = settings;
            _longPressButton = null;
            _longPressToken++;
        }
    }

    // ---------------- キーボード ----------------

    /// <param name="key">キー名（"Space", "A", "F1" …）。修飾キー自身は呼ばなくてよい。</param>
    public KeyDecision OnKeyDown(string key, KeyModifiers modifiers)
    {
        lock (_lock)
        {
            // 押しっぱなし中のリピート: 修飾キーの状態に関係なく抑止して無視（スペース等が漏れないように）
            if (_heldHotkeyKey is not null && string.Equals(_heldHotkeyKey, key, StringComparison.OrdinalIgnoreCase))
                return new KeyDecision(Suppress: true, Fire: false);

            if (_hotkey is { } hk
                && string.Equals(hk.Key, key, StringComparison.OrdinalIgnoreCase)
                && hk.Modifiers == modifiers)
            {
                _heldHotkeyKey = key;
                return new KeyDecision(Suppress: true, Fire: true);
            }
            return default;
        }
    }

    /// <returns>この離上を抑止するか。</returns>
    public bool OnKeyUp(string key)
    {
        lock (_lock)
        {
            if (_heldHotkeyKey is null || !string.Equals(_heldHotkeyKey, key, StringComparison.OrdinalIgnoreCase))
                return false;
            _heldHotkeyKey = null;
            return true;
        }
    }

    // ---------------- マウス ----------------

    public MouseDecision OnMouseDown(InputButton button, KeyModifiers modifiers, ScreenPoint point, long nowMs)
    {
        lock (_lock)
        {
            if (button == InputButton.Middle) _middleHeld = true;
            // 1. 修飾キー + クリック（押下・離上とも抑止）
            foreach (var t in Enabled(MouseGesture.Click))
            {
                if (Matches(t, button) && t.Modifiers == modifiers && DesktopOk(point))
                {
                    _suppressedDown.Add(button);
                    return new MouseDecision(Suppress: true, Fire: true, PassedThrough: false);
                }
            }

            int? longPressToken = null;

            // 2. 長押し: 短押しをアプリに通す必要があるので押下は抑止できない（SPEC §4.2）
            foreach (var t in Enabled(MouseGesture.LongPress))
            {
                if (Matches(t, button) && t.Modifiers == modifiers)
                {
                    _longPressButton = button;
                    _longPressPoint = point;
                    longPressToken = ++_longPressToken;
                    break;
                }
            }

            // 3. 左右同時
            if (button is InputButton.Left or InputButton.Right && Enabled(MouseGesture.LeftRightTogether).Any())
            {
                ref long mine = ref button == InputButton.Left ? ref _lastLeftDownMs : ref _lastRightDownMs;
                long other = button == InputButton.Left ? _lastRightDownMs : _lastLeftDownMs;
                mine = nowMs;
                if (nowMs - other <= _mouse.SimultaneousWindowMs && DesktopOk(point))
                {
                    _lastLeftDownMs = _lastRightDownMs = NeverPressed; // 1 回の同時押しで 1 回だけ
                    return new MouseDecision(Suppress: false, Fire: true, PassedThrough: false, longPressToken);
                }
            }

            return new MouseDecision(Suppress: false, Fire: false, PassedThrough: true, longPressToken);
        }
    }

    public MouseDecision OnMouseUp(InputButton button)
    {
        lock (_lock)
        {
            if (button == InputButton.Middle) _middleHeld = false;
            if (_longPressButton == button)
            {
                _longPressButton = null;
                _longPressToken++; // 進行中の長押し判定を無効化
            }
            if (_suppressedDown.Remove(button))
                return new MouseDecision(Suppress: true, Fire: false, PassedThrough: false);
            return new MouseDecision(Suppress: false, Fire: false, PassedThrough: true);
        }
    }

    /// <summary>
    /// ホイール回転（SPEC §4.2「ホイールクリック + 回転」）。中ボタンを押している間の縦の回転だけが対象で、その回転は抑止する。
    /// 中ボタンの押下・離上そのものは下のアプリへ通す（長押しと同じ妥協: 回さずに離せば普通の中クリック）。
    /// </summary>
    /// <param name="rotation">回転量（正 = 手前に回す = 下へスクロール = 次のページ）。</param>
    public WheelDecision OnMouseWheel(int rotation, KeyModifiers modifiers, ScreenPoint point)
    {
        lock (_lock)
        {
            if (!_middleHeld || rotation == 0) return new WheelDecision(false, 0);
            if (!Enabled(MouseGesture.WheelClickRotate).Any(t => t.Modifiers == modifiers) || !DesktopOk(point))
                return new WheelDecision(false, 0);
            return new WheelDecision(true, rotation > 0 ? 1 : -1);
        }
    }

    /// <returns>長押し成立（ボタンがまだ押されたまま）なら true と押下位置。</returns>
    public bool OnLongPressElapsed(int token, out ScreenPoint point)
    {
        lock (_lock)
        {
            point = _longPressPoint;
            if (token != _longPressToken || _longPressButton is null || !DesktopOk(point)) return false;
            _longPressButton = null;
            return true;
        }
    }

    public int LongPressMs
    {
        get { lock (_lock) return _mouse.LongPressMs; }
    }

    private IEnumerable<MouseTriggerSetting> Enabled(MouseGesture gesture) =>
        _mouse.Mouse.Where(t => t.Enabled && t.Gesture == gesture);

    private static bool Matches(MouseTriggerSetting t, InputButton button) => (t.Button, button) switch
    {
        (MouseButtonKind.Left, InputButton.Left) => true,
        (MouseButtonKind.Middle, InputButton.Middle) => true,
        (MouseButtonKind.X1, InputButton.X1) => true,
        (MouseButtonKind.X2, InputButton.X2) => true,
        _ => false,
    };

    private bool DesktopOk(ScreenPoint p) => !_mouse.DesktopOnly || _isDesktop is null || _isDesktop(p);
}
