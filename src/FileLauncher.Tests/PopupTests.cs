using FileLauncher.Core.Input;
using FileLauncher.Core.Model;
using FileLauncher.Core.Popup;

namespace FileLauncher.Tests;

public class OutsideClickTrackerTests
{
    private static readonly ScreenRect Board = new(100, 100, 400, 300);

    [Fact]
    public void 盤面外をクリックしたら離した時点で閉じる()
    {
        var t = new OutsideClickTracker();

        t.OnDown(new(50, 50), true, Board);
        Assert.True(t.IsPressedOutside);
        Assert.Equal(OutsideClickResult.CloseClick, t.OnUp(new(52, 51), true, Board));
        Assert.False(t.IsPressedOutside);
    }

    [Fact]
    public void 盤面外から盤面へドラッグして離したら開いたまま()
    {
        var t = new OutsideClickTracker();

        t.OnDown(new(50, 50), true, Board);
        Assert.Equal(OutsideClickResult.KeepOpenDroppedOnBoard, t.OnUp(new(200, 200), true, Board));
    }

    [Fact]
    public void 盤面外でドラッグして盤面外で離したら閉じる()
    {
        var t = new OutsideClickTracker();

        t.OnDown(new(50, 50), true, Board);
        Assert.Equal(OutsideClickResult.CloseDragEndedOutside, t.OnUp(new(50, 90), true, Board));
    }

    [Fact]
    public void 盤面内の操作や非表示中のクリックは無視()
    {
        var t = new OutsideClickTracker();

        t.OnDown(new(200, 200), true, Board);
        Assert.False(t.IsPressedOutside);
        Assert.Equal(OutsideClickResult.None, t.OnUp(new(800, 800), true, Board));

        t.OnDown(new(50, 50), false, Board);
        Assert.Equal(OutsideClickResult.None, t.OnUp(new(50, 50), true, Board));
    }

    [Fact]
    public void 押下と離上の間に盤面が閉じていたら何もしない()
    {
        var t = new OutsideClickTracker();

        t.OnDown(new(50, 50), true, Board);
        Assert.Equal(OutsideClickResult.None, t.OnUp(new(50, 50), false, Board));
    }
}

public class PopupPlacementTests
{
    private static readonly ScreenRect Primary = new(0, 0, 2560, 1392);
    private static readonly ScreenRect Second = new(2560, 0, 2560, 1392);

    private static ScreenRect AreaAt(ScreenPoint p) => p.X >= 2560 ? Second : Primary;

    [Fact]
    public void カーソル位置では横中央_上端の少し下にカーソルが来る()
    {
        var p = PopupPlacement.Compute(new PopupPlacementSettings(), new(1000, 500), 400, 300, AreaAt, Primary);

        Assert.Equal(new ScreenPoint(800, 500 - PopupPlacement.CursorInset), p);
    }

    [Theory]
    [InlineData(CursorAnchor.TopLeft, 980, 480)]
    [InlineData(CursorAnchor.Top, 800, 480)]
    [InlineData(CursorAnchor.TopRight, 620, 480)]
    [InlineData(CursorAnchor.Left, 980, 350)]
    [InlineData(CursorAnchor.Center, 800, 350)]
    [InlineData(CursorAnchor.Right, 620, 350)]
    [InlineData(CursorAnchor.BottomLeft, 980, 220)]
    [InlineData(CursorAnchor.Bottom, 800, 220)]
    [InlineData(CursorAnchor.BottomRight, 620, 220)]
    public void カーソル位置では選んだアンカーの点にカーソルが来る_辺と角は外形から20内側(CursorAnchor anchor, int x, int y)
    {
        Assert.Equal(new ScreenPoint(x, y), PopupPlacement.Compute(new PopupPlacementSettings(), new(1000, 500), 400, 300, AreaAt, Primary, anchor));
    }

    [Fact]
    public void 寄せ量は拡大率を掛けて四捨五入し_座標未保存の退避でもアンカーを使う()
    {
        Assert.Equal(20, PopupPlacement.InsetFor(1));
        Assert.Equal(30, PopupPlacement.InsetFor(1.5));
        Assert.Equal(25, PopupPlacement.InsetFor(1.25));
        Assert.Equal(new ScreenPoint(970, 470), PopupPlacement.Compute(new PopupPlacementSettings(), new(1000, 500), 400, 300, AreaAt, Primary, CursorAnchor.TopLeft, scaling: 1.5));
        var unsaved = new PopupPlacementSettings { Position = PopupPosition.Fixed };
        Assert.Equal(new ScreenPoint(800, 350), PopupPlacement.Compute(unsaved, new(1000, 500), 400, 300, AreaAt, Primary, CursorAnchor.Center));
    }

    [Theory]
    [InlineData(CursorAnchor.TopLeft, 2550, 1380)]
    [InlineData(CursorAnchor.BottomRight, 5, 5)]
    [InlineData(CursorAnchor.Center, 2559, 0)]
    public void どのアンカーでも作業領域からはみ出さない(CursorAnchor anchor, int cx, int cy)
    {
        var p = PopupPlacement.Compute(new PopupPlacementSettings(), new(cx, cy), 400, 300, AreaAt, Primary, anchor);
        var area = AreaAt(new(cx, cy));
        Assert.InRange(p.X, area.X, area.Right - 400);
        Assert.InRange(p.Y, area.Y, area.Bottom - 300);
    }

    [Fact]
    public void アンカーはキーボードとマウス共通の1つで_JSONでcamelCaseになり_キーが無ければ上中央()
    {
        var s = new AppSettings();
        s.Popup.CursorAnchor = CursorAnchor.BottomRight;
        string json = System.Text.Json.JsonSerializer.Serialize(s, FileLauncher.Core.Storage.JsonDefaults.Options);
        Assert.Contains("\"cursorAnchor\": \"bottomRight\"", json);
        var empty = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}", FileLauncher.Core.Storage.JsonDefaults.Options)!;
        Assert.Equal(CursorAnchor.Top, empty.Popup.CursorAnchor);
        // 組ごとに持っていた頃の旧キー（未リリース）は読み飛ばす
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            "{\"popup\":{\"keyboard\":{\"cursorAnchor\":\"bottomRight\"},\"mouse\":{\"cursorAnchor\":\"center\"}}}", FileLauncher.Core.Storage.JsonDefaults.Options)!;
        Assert.Equal(CursorAnchor.Top, old.Popup.CursorAnchor);
    }

    [Fact]
    public void 画面の右下隅では作業領域内に収める()
    {
        var p = PopupPlacement.Compute(new PopupPlacementSettings(), new(2550, 1380), 400, 300, AreaAt, Primary);

        Assert.Equal(new ScreenPoint(2560 - 400, 1392 - 300), p);
    }

    [Fact]
    public void 二枚目のモニタではそのモニタに収める()
    {
        var p = PopupPlacement.Compute(new PopupPlacementSettings(), new(2570, 10), 400, 300, AreaAt, Primary);

        Assert.Equal(new ScreenPoint(2560, 0), p);
    }

    [Fact]
    public void 前回位置はその座標のモニタに収める_未保存ならカーソル位置()
    {
        var s = new PopupPlacementSettings { Position = PopupPosition.LastPosition, X = 5000, Y = 100 };
        Assert.Equal(new ScreenPoint(5120 - 400, 100), PopupPlacement.Compute(s, new(10, 10), 400, 300, AreaAt, Primary));

        var unsaved = new PopupPlacementSettings { Position = PopupPosition.LastPosition };
        Assert.Equal(new ScreenPoint(810, 480), PopupPlacement.Compute(unsaved, new(1010, 500), 400, 300, AreaAt, Primary));
    }

    [Fact]
    public void 画面中央はプライマリモニタの中央()
    {
        var s = new PopupPlacementSettings { Position = PopupPosition.ScreenCenter };

        Assert.Equal(new ScreenPoint(1080, 546), PopupPlacement.Compute(s, new(3000, 10), 400, 300, AreaAt, Primary));
    }
}

public class MouseLeaveTrackerTests
{
    private static readonly ScreenRect Board = new(100, 100, 200, 100); // x 100〜299, y 100〜199

    [Fact]
    public void カーソルが一度も盤面に入っていなければ遠くても閉じない()
    {
        var t = new MouseLeaveTracker();
        Assert.False(t.Update(new ScreenPoint(1000, 1000), Board, 200));
    }

    [Fact]
    public void 入ってから閾値を超えて離れたら閉じる()
    {
        var t = new MouseLeaveTracker();
        Assert.False(t.Update(new ScreenPoint(150, 150), Board, 200));
        Assert.False(t.Update(new ScreenPoint(299 + 200, 150), Board, 200)); // ちょうど閾値は閉じない
        Assert.True(t.Update(new ScreenPoint(299 + 201, 150), Board, 200));
    }

    [Fact]
    public void 斜め方向は角からの距離で測る()
    {
        Assert.Equal(5, MouseLeaveTracker.DistanceOutside(new ScreenPoint(299 + 3, 199 + 4), Board));
        Assert.Equal(0, MouseLeaveTracker.DistanceOutside(new ScreenPoint(200, 150), Board));
        Assert.Equal(10, MouseLeaveTracker.DistanceOutside(new ScreenPoint(150, 90), Board));
    }

    [Fact]
    public void Resetすると入ったかどうかを忘れる()
    {
        var t = new MouseLeaveTracker();
        t.Update(new ScreenPoint(150, 150), Board, 200);
        t.Reset();
        Assert.False(t.Update(new ScreenPoint(1000, 1000), Board, 200));
    }
}


public class PopupSettingsTests
{
    [Fact]
    public void 前回位置は前回位置を選んでいる組すべてに書く()
    {
        var p = new PopupSettings();
        p.Keyboard.Position = PopupPosition.LastPosition;
        p.Mouse.Position = PopupPosition.LastPosition;

        Assert.True(p.RememberLastPosition(10, 20));
        Assert.Equal((10, 20, 10, 20), (p.Keyboard.X, p.Keyboard.Y, p.Mouse.X, p.Mouse.Y));
    }

    [Fact]
    public void 片方だけ前回位置ならもう片方の座標は変えない_固定座標も変えない()
    {
        var p = new PopupSettings();
        p.Keyboard.Position = PopupPosition.LastPosition;
        p.Mouse.Position = PopupPosition.Fixed;
        p.Mouse.X = 5;
        p.Mouse.Y = 6;

        p.RememberLastPosition(10, 20);

        Assert.Equal((10, 20), (p.Keyboard.X, p.Keyboard.Y));
        Assert.Equal((5, 6), (p.Mouse.X, p.Mouse.Y));
    }

    [Fact]
    public void どちらも前回位置でなければ何も書かない()
    {
        var p = new PopupSettings();
        Assert.False(p.RememberLastPosition(10, 20));
        Assert.Null(p.Keyboard.X);
        Assert.Null(p.Mouse.X);
    }

    [Fact]
    public void ホットキーはキーボードの組_それ以外はマウスの組を使う()
    {
        var p = new PopupSettings();
        Assert.Same(p.Keyboard, p.PlacementFor(PopupTriggerKind.Keyboard));
        Assert.Same(p.Mouse, p.PlacementFor(PopupTriggerKind.Mouse));
    }
}

public class ResidentLayoutTests
{
    private static readonly ScreenRect Work = new(0, 25, 1920, 1015); // y 25〜1039（メニューバー / タスクバー除く）

    [Theory]
    [InlineData(0, 300, ScreenEdge.Left)]
    [InlineData(8, 300, ScreenEdge.Left)]
    [InlineData(9, 300, ScreenEdge.None)]
    [InlineData(1920 - 400, 300, ScreenEdge.Right)]
    [InlineData(500, 25, ScreenEdge.Top)]
    [InlineData(500, 1040 - 300 - 5, ScreenEdge.Bottom)]
    public void 作業領域の辺から8px以内なら貼り付いているとみなす(int x, int y, ScreenEdge expected)
    {
        Assert.Equal(expected, ResidentLayout.StuckEdge(new ScreenRect(x, y, 400, 300), Work));
    }

    [Fact]
    public void 収納すると帯4pxだけ作業領域に残る()
    {
        var board = new ScreenRect(0, 300, 400, 300);
        Assert.Equal(new ScreenPoint(-396, 300), ResidentLayout.CollapsedPosition(board, ScreenEdge.Left, Work));
        Assert.Equal(new ScreenPoint(1916, 300), ResidentLayout.CollapsedPosition(board with { X = 1520 }, ScreenEdge.Right, Work));
        Assert.Equal(new ScreenPoint(0, 25 - 300 + 4), ResidentLayout.CollapsedPosition(board with { Y = 25 }, ScreenEdge.Top, Work));
        Assert.Equal(new ScreenPoint(0, 1040 - 4), ResidentLayout.CollapsedPosition(board with { Y = 740 }, ScreenEdge.Bottom, Work));
    }

    [Fact]
    public void 初回は主モニタの中央_保存位置が画面外なら画面内へ寄せる()
    {
        Assert.Equal(new ScreenPoint(760, 382), ResidentLayout.InitialPosition(null, null, 400, 300, _ => Work, Work));
        Assert.Equal(new ScreenPoint(1520, 740), ResidentLayout.InitialPosition(5000, 5000, 400, 300, _ => Work, Work));
        Assert.Equal(new ScreenPoint(100, 200), ResidentLayout.InitialPosition(100, 200, 400, 300, _ => Work, Work));
    }
}
