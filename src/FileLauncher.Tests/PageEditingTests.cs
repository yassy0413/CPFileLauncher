using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public class PageEditingTests
{
    private static LauncherItem Item(Page p, int r, int c, string name = "x")
    {
        var i = new LauncherItem { Row = r, Col = c, Name = name };
        p.Items.Add(i);
        return i;
    }

    [Fact]
    public void 新規ページは右隣に入り_名前は使われていない最小の番号()
    {
        var b = Board.CreateDefault();
        b.Pages.Add(new Page { Name = "Page 3" });

        int i = PageEditing.Add(b, 0, 3, 5);

        Assert.Equal(1, i);
        Assert.Equal(("Page 2", 3, 5), (b.Pages[1].Name, b.Pages[1].Rows, b.Pages[1].Cols));
    }

    [Fact]
    public void 複製はコピーの名前で右隣に入り_アイテムは新しいIdでショートカットキーも写す()
    {
        var b = Board.CreateDefault();
        var a = Item(b.Pages[0], 0, 0);
        a.Hotkey = "A";

        int i = PageEditing.Duplicate(b, 0, "{0} のコピー");

        var copy = b.Pages[i];
        Assert.Equal("Page 1 のコピー", copy.Name);
        Assert.NotEqual(a.Id, copy.Items[0].Id);
        Assert.Equal("A", copy.Items[0].Hotkey);
    }

    [Fact]
    public void 最後の1枚は削除できず_削除後は左隣を表示する()
    {
        var b = Board.CreateDefault();
        Assert.Equal(-1, PageEditing.Remove(b, 0));
        PageEditing.Add(b, 0, 4, 6);
        PageEditing.Add(b, 1, 4, 6);
        Assert.Equal(1, PageEditing.Remove(b, 2));
        Assert.Equal(0, PageEditing.Remove(b, 0));
    }

    [Fact]
    public void 左右への移動_端では動かない()
    {
        var b = Board.CreateDefault();
        PageEditing.Add(b, 0, 4, 6);
        Assert.Equal(-1, PageEditing.Move(b, 0, -1));
        Assert.Equal(1, PageEditing.Move(b, 0, +1));
        Assert.Equal("Page 1", b.Pages[1].Name);
    }

    [Fact]
    public void 行列数を減らして入りきらなければ不可_入りきるなら詰め直しの件数を返す()
    {
        var p = new Page { Rows = 2, Cols = 3 };
        Item(p, 0, 0); Item(p, 1, 2); Item(p, 1, 1);

        Assert.True(BoardEditing.CanResize(p, 1, 3, out int overflow));
        Assert.Equal(2, overflow);
        Assert.False(BoardEditing.CanResize(p, 1, 2, out _));
    }

    [Fact]
    public void 範囲外のアイテムは読み順の空きへ移り_空きが無ければ行を足す()
    {
        var p = new Page { Rows = 1, Cols = 2 };
        Item(p, 0, 0, "a");
        var b = Item(p, 5, 5, "b");
        var c = Item(p, 9, 0, "c");

        int moved = BoardEditing.ReflowOutOfRange(p, allowGrow: true);

        Assert.Equal(2, moved);
        Assert.Equal((0, 1), (b.Row, b.Col));
        Assert.Equal((1, 0), (c.Row, c.Col));
        Assert.Equal(2, p.Rows);
    }

    [Fact]
    public void 読み込み時の正規化で範囲外のアイテムは消さずに空きへ移る()
    {
        var b = Board.CreateDefault();
        var x = Item(b.Pages[0], 20, 0);
        b.Normalize();
        Assert.Contains(x, b.Pages[0].Items);
        Assert.True(x.Row < b.Pages[0].Rows);
    }

    [Fact]
    public void ページ切替の設定の既定はどちらもオン()
    {
        var s = new AppSettings();
        Assert.True(s.Board.WheelSwitchesPage && s.Board.AltNumberSwitchesPage);
    }
}
