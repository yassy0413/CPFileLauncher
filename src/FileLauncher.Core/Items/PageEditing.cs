using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>ページの操作（SPEC §6.5）。戻り値のページ番号は、操作のあと表示するページ。</summary>
public static class PageEditing
{
    /// <summary>「Page N」の N = 使われていない最小の番号。</summary>
    public static string NextDefaultName(Board board)
    {
        var names = board.Pages.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        for (int n = 1; ; n++)
            if (!names.Contains($"Page {n}")) return $"Page {n}";
    }

    /// <summary>今のページの右隣に新しいページを入れる。</summary>
    public static int Add(Board board, int currentIndex, int rows, int cols)
    {
        int index = Math.Clamp(currentIndex + 1, 0, board.Pages.Count);
        board.Pages.Insert(index, new Page
        {
            Name = NextDefaultName(board),
            Rows = Math.Clamp(rows, Page.MinGrid, Page.MaxGrid),
            Cols = Math.Clamp(cols, Page.MinGrid, Page.MaxGrid),
        });
        return index;
    }

    /// <summary>
    /// 写しを右隣に。名前は copyNameFormat（{0} = 元の名前。UI の言語に合わせて App が渡す）。
    /// アイテムは新しい ID で写す（ショートカットキーも写す: 別ページなので重ならない）。
    /// </summary>
    public static int Duplicate(Board board, int index, string copyNameFormat = "{0} copy")
    {
        var src = board.Pages[index];
        var copy = new Page
        {
            Name = string.Format(copyNameFormat, src.Name),
            Rows = src.Rows,
            Cols = src.Cols,
            ButtonSize = src.ButtonSize,
            Items = src.Items.Select(i => BoardEditing.Clone(i, keepHotkey: true)).ToList(),
        };
        board.Pages.Insert(index + 1, copy);
        return index + 1;
    }

    public static bool CanRemove(Board board) => board.Pages.Count > 1;

    /// <summary>削除して、次に表示するページ（左隣、無ければ 0）を返す。最後の 1 枚なら消さずに -1。</summary>
    public static int Remove(Board board, int index)
    {
        if (!CanRemove(board)) return -1;
        board.Pages.RemoveAt(index);
        return Math.Max(0, index - 1);
    }

    /// <summary>左（-1）/ 右（+1）へ動かす。端なら動かさずに -1。動かしたら新しい位置。</summary>
    public static int Move(Board board, int index, int delta)
    {
        int to = index + delta;
        if (to < 0 || to >= board.Pages.Count || delta == 0) return -1;
        var page = board.Pages[index];
        board.Pages.RemoveAt(index);
        board.Pages.Insert(to, page);
        return to;
    }
}
