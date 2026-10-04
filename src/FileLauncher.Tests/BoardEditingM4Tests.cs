using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public class BoardEditingM4Tests
{
    private static Page NewPage() => new() { Name = "P", Rows = 2, Cols = 3 };

    private static LauncherItem Item(Page page, int row, int col, string name = "x")
    {
        var item = new LauncherItem { Row = row, Col = col, Name = name, Target = "/" + name };
        page.Items.Add(item);
        return item;
    }

    [Fact]
    public void 空スロットへの移動で位置が変わる()
    {
        var p = NewPage();
        var a = Item(p, 0, 0);
        Assert.True(BoardEditing.MoveOrSwap(p, a, p, 1, 2));
        Assert.Equal((1, 2), (a.Row, a.Col));
    }

    [Fact]
    public void 別のアイテムの上への移動で入れ替わる()
    {
        var p = NewPage();
        var a = Item(p, 0, 0, "a");
        var b = Item(p, 1, 1, "b");
        BoardEditing.MoveOrSwap(p, a, p, 1, 1);
        Assert.Equal((1, 1, 0, 0), (a.Row, a.Col, b.Row, b.Col));
    }

    [Fact]
    public void 同じセルへの移動は何もしない()
    {
        var p = NewPage();
        var a = Item(p, 0, 0);
        Assert.False(BoardEditing.MoveOrSwap(p, a, p, 0, 0));
    }

    [Fact]
    public void 別ページへの移動で元のページから消えて移動先に入る_入れ替え相手は元のページへ()
    {
        var p1 = NewPage();
        var p2 = NewPage();
        var a = Item(p1, 0, 0, "a");
        var b = Item(p2, 0, 1, "b");
        BoardEditing.MoveOrSwap(p1, a, p2, 0, 1);
        Assert.Contains(a, p2.Items);
        Assert.DoesNotContain(a, p1.Items);
        Assert.Contains(b, p1.Items);
        Assert.Equal((0, 0), (b.Row, b.Col));
    }

    [Fact]
    public void 複製は新しいIdで色や引数を引き継ぎ_ショートカットキーは引き継がない()
    {
        var p = NewPage();
        var a = Item(p, 0, 0);
        a.Color = ItemColor.Purple;
        a.Args = "--x";
        a.Hotkey = "A";

        var copy = BoardEditing.Duplicate(a, p, 1, 0)!;

        Assert.NotEqual(a.Id, copy.Id);
        Assert.Equal((ItemColor.Purple, "--x", (string?)null), (copy.Color, copy.Args, copy.Hotkey));
        Assert.Equal((1, 0), (copy.Row, copy.Col));
        Assert.Equal(2, p.Items.Count);
        Assert.Null(BoardEditing.Duplicate(a, p, 1, 0)); // 占有済み
    }

    [Fact]
    public void 削除と最初の空きセル()
    {
        var p = NewPage();
        var a = Item(p, 0, 0);
        Item(p, 0, 1);
        Assert.Equal((0, 2), BoardEditing.FirstEmpty(p));
        Assert.True(BoardEditing.Remove(p, a));
        Assert.False(BoardEditing.Remove(p, a));
        Assert.Equal((0, 0), BoardEditing.FirstEmpty(p));
    }

    [Theory]
    [InlineData(-1, 10, null, null)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(99.9, 49.9, 0, 0)]
    [InlineData(100, 50, 1, 1)]
    [InlineData(299, 99, 1, 2)]
    [InlineData(300, 10, null, null)]
    public void セルの判定は範囲外と負を除く(double x, double y, int? row, int? col)
    {
        var cell = BoardDragRules.CellAt(x, y, 100, 50, rows: 2, cols: 3);
        Assert.Equal(row is null ? null : (row.Value, col!.Value), cell);
    }

    [Theory]
    [InlineData(DragTargetKind.Slot, false, false, false, DragOutcome.Move)]
    [InlineData(DragTargetKind.Slot, true, false, false, DragOutcome.Swap)]
    [InlineData(DragTargetKind.Slot, false, false, true, DragOutcome.Duplicate)]
    [InlineData(DragTargetKind.Slot, true, false, true, DragOutcome.Cancel)]
    [InlineData(DragTargetKind.Slot, false, true, false, DragOutcome.Cancel)]
    [InlineData(DragTargetKind.Tab, false, false, false, DragOutcome.MoveToPage)]
    [InlineData(DragTargetKind.Tab, false, false, true, DragOutcome.DuplicateToPage)]
    [InlineData(DragTargetKind.Outside, false, false, false, DragOutcome.Delete)]
    [InlineData(DragTargetKind.Outside, false, false, true, DragOutcome.Cancel)]
    [InlineData(DragTargetKind.None, false, false, false, DragOutcome.Cancel)]
    public void ドラッグの結果の判定(DragTargetKind target, bool occupied, bool sameCell, bool copy, DragOutcome expected)
    {
        Assert.Equal(expected, BoardDragRules.Classify(target, occupied, sameCell, copy));
    }
}

public class BoardBackupIntervalTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _dir.Dispose();

    private int BackupCount() =>
        Directory.Exists(_dir.File("backup")) ? Directory.GetFiles(_dir.File("backup"), "board-*.json").Length : 0;

    [Fact]
    public void 盤面のバックアップは起動後の最初の保存で作り_10分以内は作らず_10分たつとまた作る()
    {
        var store = new AppDataStore(new DataPaths(_dir.Path, IsPortable: false), _time);
        store.LoadAll();
        var board = Board.CreateDefault();
        store.SaveBoard(board); // 本体がまだ無いのでバックアップ無し
        store.SaveBoard(board);
        Assert.Equal(1, BackupCount()); // 起動後の最初

        _time.Advance(TimeSpan.FromMinutes(5));
        store.SaveBoard(board);
        Assert.Equal(1, BackupCount());

        _time.Advance(TimeSpan.FromMinutes(6));
        store.SaveBoard(board);
        Assert.Equal(2, BackupCount());
    }
}
