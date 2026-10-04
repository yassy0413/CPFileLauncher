using FileLauncher.Core.Input;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public class TriggerDetectorTests
{
    private static readonly ScreenPoint P = new(100, 200);
    private const KeyModifiers CtrlAlt = KeyModifiers.Ctrl | KeyModifiers.Alt;

    private static TriggerDetector WithDefaults(Func<ScreenPoint, bool>? isDesktop = null)
    {
        var s = new AppSettings().Triggers;
        var d = new TriggerDetector(isDesktop);
        d.SetHotkey(s.Hotkey);
        d.SetMouse(s);
        return d;
    }

    private static TriggerDetector WithMouse(params MouseTriggerSetting[] triggers)
    {
        var d = new TriggerDetector();
        d.SetMouse(new TriggerSettings { Mouse = triggers.ToList() });
        return d;
    }

    // ---------------- ホットキー ----------------

    [Fact]
    public void 既定のCtrlAltSpaceで発火し_スペースは抑止する()
    {
        var d = WithDefaults();

        Assert.Equal(new KeyDecision(Suppress: true, Fire: true), d.OnKeyDown("Space", CtrlAlt));
    }

    [Fact]
    public void 修飾キーが違えば素通し()
    {
        var d = WithDefaults();

        Assert.Equal(default, d.OnKeyDown("Space", KeyModifiers.Ctrl));
        Assert.Equal(default, d.OnKeyDown("Space", CtrlAlt | KeyModifiers.Shift));
        Assert.Equal(default, d.OnKeyDown("A", CtrlAlt));
    }

    [Fact]
    public void 押しっぱなしのキーリピートは発火しないが抑止は続け_離すと次を受け付ける()
    {
        var d = WithDefaults();
        d.OnKeyDown("Space", CtrlAlt);

        for (int i = 0; i < 19; i++)
            Assert.Equal(new KeyDecision(Suppress: true, Fire: false), d.OnKeyDown("Space", CtrlAlt));
        // 修飾キーを先に離してもリピート分のスペースは漏らさない
        Assert.Equal(new KeyDecision(Suppress: true, Fire: false), d.OnKeyDown("Space", KeyModifiers.None));

        Assert.True(d.OnKeyUp("Space"));
        Assert.True(d.OnKeyDown("Space", CtrlAlt).Fire);
    }

    [Fact]
    public void 関係ないキーの離上は抑止しない()
    {
        var d = WithDefaults();

        Assert.False(d.OnKeyUp("Space"));
        Assert.False(d.OnKeyUp("A"));
    }

    [Fact]
    public void ホットキー無効なら発火しない()
    {
        var d = new TriggerDetector();
        d.SetHotkey(new HotkeySetting { Enabled = false });

        Assert.Equal(default, d.OnKeyDown("Space", CtrlAlt));
    }

    // ---------------- 修飾キー + クリック ----------------

    [Fact]
    public void 既定のCtrl中クリックで発火し_押下と離上を抑止する()
    {
        var d = WithDefaults();

        var down = d.OnMouseDown(InputButton.Middle, KeyModifiers.Ctrl, P, 0);
        Assert.True(down.Fire);
        Assert.True(down.Suppress);
        Assert.False(down.PassedThrough);

        // Ctrl を先に離していても離上は対で抑止
        var up = d.OnMouseUp(InputButton.Middle);
        Assert.True(up.Suppress);
        Assert.False(up.PassedThrough);
    }

    [Fact]
    public void 修飾キーなしの中クリックや左クリックは素通しして外クリック判定に回す()
    {
        var d = WithDefaults();

        foreach (var (b, m) in new[] { (InputButton.Middle, KeyModifiers.None), (InputButton.Left, KeyModifiers.Ctrl), (InputButton.Left, KeyModifiers.None) })
        {
            var down = d.OnMouseDown(b, m, P, 0);
            Assert.False(down.Fire);
            Assert.False(down.Suppress);
            Assert.True(down.PassedThrough);
            var up = d.OnMouseUp(b);
            Assert.False(up.Suppress);
            Assert.True(up.PassedThrough);
        }
    }

    [Fact]
    public void サイドボタンも修飾キー付きクリックとして使える()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.Click, Button = MouseButtonKind.X1, Modifiers = KeyModifiers.None });

        Assert.True(d.OnMouseDown(InputButton.X1, KeyModifiers.None, P, 0).Fire);
        Assert.False(d.OnMouseDown(InputButton.X2, KeyModifiers.None, P, 0).Fire);
    }

    // ---------------- 長押し ----------------

    [Fact]
    public void 長押しは押下を通しつつ判定を開始し_押したままなら成立()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.LongPress, Button = MouseButtonKind.Middle });

        var down = d.OnMouseDown(InputButton.Middle, KeyModifiers.None, P, 0);
        Assert.False(down.Suppress);
        Assert.False(down.Fire);
        Assert.NotNull(down.LongPressToken);

        Assert.True(d.OnLongPressElapsed(down.LongPressToken!.Value, out var at));
        Assert.Equal(P, at);
        // 同じ押下で 2 回は成立しない
        Assert.False(d.OnLongPressElapsed(down.LongPressToken!.Value, out _));
    }

    [Fact]
    public void 修飾キー付きの長押しは同じ修飾キーのときだけ判定を始める()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.LongPress, Button = MouseButtonKind.Middle, Modifiers = KeyModifiers.Ctrl });

        Assert.Null(d.OnMouseDown(InputButton.Middle, KeyModifiers.None, P, 0).LongPressToken);
        d.OnMouseUp(InputButton.Middle);
        var down = d.OnMouseDown(InputButton.Middle, KeyModifiers.Ctrl, P, 0);

        Assert.True(d.OnLongPressElapsed(down.LongPressToken!.Value, out _));
    }

    [Fact]
    public void 長押し判定中に離したら成立しない()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.LongPress, Button = MouseButtonKind.Middle });

        var token = d.OnMouseDown(InputButton.Middle, KeyModifiers.None, P, 0).LongPressToken!.Value;
        d.OnMouseUp(InputButton.Middle);

        Assert.False(d.OnLongPressElapsed(token, out _));
    }

    // ---------------- ホイールクリック + 回転 ----------------

    [Fact]
    public void 中ボタンを押している間の回転だけ抑止して発火し_押下自体は通す()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.WheelClickRotate, Button = MouseButtonKind.Middle });

        Assert.Equal(new WheelDecision(false, 0), d.OnMouseWheel(3, KeyModifiers.None, P)); // 押していない
        var down = d.OnMouseDown(InputButton.Middle, KeyModifiers.None, P, 0);
        Assert.False(down.Suppress); // 押下は下のアプリへ通す
        Assert.Equal(new WheelDecision(true, 1), d.OnMouseWheel(3, KeyModifiers.None, P));
        Assert.Equal(new WheelDecision(true, -1), d.OnMouseWheel(-3, KeyModifiers.None, P));
        d.OnMouseUp(InputButton.Middle);
        Assert.Equal(new WheelDecision(false, 0), d.OnMouseWheel(3, KeyModifiers.None, P));
    }

    [Fact]
    public void ホイールクリック回転が無効なら回転は通す_修飾キーも一致が必要()
    {
        var off = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.Click, Button = MouseButtonKind.Middle, Modifiers = KeyModifiers.Ctrl });
        off.OnMouseDown(InputButton.Middle, KeyModifiers.None, P, 0);
        Assert.Equal(new WheelDecision(false, 0), off.OnMouseWheel(3, KeyModifiers.None, P));

        var withCtrl = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.WheelClickRotate, Button = MouseButtonKind.Middle, Modifiers = KeyModifiers.Ctrl });
        withCtrl.OnMouseDown(InputButton.Middle, KeyModifiers.Ctrl, P, 0);
        Assert.Equal(0, withCtrl.OnMouseWheel(3, KeyModifiers.None, P).PageDelta);
        Assert.Equal(1, withCtrl.OnMouseWheel(3, KeyModifiers.Ctrl, P).PageDelta);
    }

    // ---------------- 左右同時 ----------------

    [Fact]
    public void 左右同時は判定ウィンドウ内なら発火_単発クリックでは発火しない()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.LeftRightTogether });

        // 初回の単発左クリック（番兵値のオーバーフローで誤発火したスパイクのバグの回帰テスト）
        Assert.False(d.OnMouseDown(InputButton.Left, KeyModifiers.None, P, 5_000).Fire);
        d.OnMouseUp(InputButton.Left);
        Assert.False(d.OnMouseDown(InputButton.Right, KeyModifiers.None, P, 5_500).Fire); // 500ms 後 → 同時ではない
        d.OnMouseUp(InputButton.Right);

        Assert.False(d.OnMouseDown(InputButton.Left, KeyModifiers.None, P, 10_000).Fire);
        Assert.True(d.OnMouseDown(InputButton.Right, KeyModifiers.None, P, 10_080).Fire);
    }

    [Fact]
    public void 左右同時は1回の同時押しで1回だけ発火()
    {
        var d = WithMouse(new MouseTriggerSetting { Gesture = MouseGesture.LeftRightTogether });

        d.OnMouseDown(InputButton.Left, KeyModifiers.None, P, 0);
        Assert.True(d.OnMouseDown(InputButton.Right, KeyModifiers.None, P, 10).Fire);
        Assert.False(d.OnMouseDown(InputButton.Left, KeyModifiers.None, P, 20).Fire);
    }

    [Fact]
    public void 左右同時は無効なら発火しない()
    {
        var d = WithDefaults();

        d.OnMouseDown(InputButton.Left, KeyModifiers.None, P, 0);
        Assert.False(d.OnMouseDown(InputButton.Right, KeyModifiers.None, P, 10).Fire);
    }

    // ---------------- デスクトップ上のみ ----------------

    [Fact]
    public void デスクトップ上のみが有効ならデスクトップ以外では素通し()
    {
        var desktop = new ScreenPoint(1, 1);
        var d = new TriggerDetector(isDesktop: p => p == desktop);
        var s = new AppSettings().Triggers;
        s.DesktopOnly = true;
        d.SetMouse(s);

        var onApp = d.OnMouseDown(InputButton.Middle, KeyModifiers.Ctrl, P, 0);
        Assert.False(onApp.Fire);
        Assert.False(onApp.Suppress);
        d.OnMouseUp(InputButton.Middle);

        Assert.True(d.OnMouseDown(InputButton.Middle, KeyModifiers.Ctrl, desktop, 0).Fire);
    }
}
