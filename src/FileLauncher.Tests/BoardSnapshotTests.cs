using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public sealed class BoardSnapshotTests
{
    private static Board Sample()
    {
        var board = Board.CreateDefault();
        board.Pages[0].Items.Add(new LauncherItem { Name = "a", Target = "/tmp/a", Row = 0, Col = 1, Color = ItemColor.Red });
        PageEditing.Add(board, 0, 2, 3);
        board.Pages[1].Items.Add(new LauncherItem { Name = "b", Target = "/tmp/b", Row = 1, Col = 2 });
        return board;
    }

    [Fact]
    public void 写しから戻すと同じBoardインスタンスのまま内容が戻る()
    {
        var board = Sample();
        var snap = BoardSnapshot.Capture(board);
        var item = board.Pages[0].Items[0];
        item.Row = 3; item.Color = null;
        board.Pages[0].Items.Clear();

        var before = board;
        BoardSnapshot.Restore(board, snap);

        Assert.Same(before, board);
        Assert.Equal(snap, BoardSnapshot.Capture(board));
        Assert.Equal((0, 1, ItemColor.Red), (board.Pages[0].Items[0].Row, board.Pages[0].Items[0].Col, board.Pages[0].Items[0].Color));
    }

    [Fact]
    public void アイテム入りのページを削除しても写しから戻せる()
    {
        var board = Sample();
        var snap = BoardSnapshot.Capture(board);

        PageEditing.Remove(board, 1);
        Assert.Single(board.Pages);

        BoardSnapshot.Restore(board, snap);
        Assert.Equal(2, board.Pages.Count);
        Assert.Equal("b", Assert.Single(board.Pages[1].Items).Name);
    }
}
