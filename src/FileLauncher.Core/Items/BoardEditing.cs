using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>盤面への配置（SPEC §6.1）。</summary>
public static class BoardEditing
{
    /// <summary>
    /// (startRow, startCol) から右 → 次の行の順に空きスロットを探す。見つかった分だけ返す（count 未満もあり得る）。
    /// 開始位置より前（左上側）の空きは使わない（ドロップ位置から並べるため）。
    /// </summary>
    public static List<(int Row, int Col)> FindEmptySlots(Page page, int startRow, int startCol, int count)
    {
        var occupied = page.Items.Select(i => (i.Row, i.Col)).ToHashSet();
        var result = new List<(int, int)>();
        for (int index = startRow * page.Cols + startCol; index < page.Rows * page.Cols && result.Count < count; index++)
        {
            var cell = (index / page.Cols, index % page.Cols);
            if (!occupied.Contains(cell)) result.Add(cell);
        }
        return result;
    }

    /// <summary>開始位置以降の空きが足りないとき、全部置くのに追加で必要な行数（上限 12 行は考慮しない）。</summary>
    public static int ExtraRowsNeeded(Page page, int startRow, int startCol, int count)
    {
        int free = FindEmptySlots(page, startRow, startCol, count).Count;
        int missing = count - free;
        return missing <= 0 ? 0 : (missing + page.Cols - 1) / page.Cols;
    }

    /// <summary>開始位置から空きスロットへ順に置く。置けた件数を返す（置けなかった分は items の後ろ側）。</summary>
    public static int Place(Page page, int startRow, int startCol, IReadOnlyList<LauncherItem> items)
    {
        var slots = FindEmptySlots(page, startRow, startCol, items.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            items[i].Row = slots[i].Row;
            items[i].Col = slots[i].Col;
            page.Items.Add(items[i]);
        }
        return slots.Count;
    }

    public static LauncherItem? ItemAt(Page page, int row, int col) =>
        page.Items.FirstOrDefault(i => i.Row == row && i.Col == col);

    // ---------------- 盤面内の編集（M4。SPEC §6.2 / §6.3） ----------------

    /// <summary>
    /// アイテムを (toPage, row, col) へ移す。移動先に別のアイテムがあれば入れ替える（入れ替え相手は元の位置へ）。
    /// 同じページの同じセルなら何もしない（false）。
    /// </summary>
    public static bool MoveOrSwap(Page fromPage, LauncherItem item, Page toPage, int row, int col)
    {
        if (ReferenceEquals(fromPage, toPage) && item.Row == row && item.Col == col) return false;
        var other = ItemAt(toPage, row, col);
        if (other is not null && !ReferenceEquals(other, item))
        {
            other.Row = item.Row;
            other.Col = item.Col;
            if (!ReferenceEquals(fromPage, toPage))
            {
                toPage.Items.Remove(other);
                fromPage.Items.Add(other);
            }
        }
        if (!ReferenceEquals(fromPage, toPage))
        {
            fromPage.Items.Remove(item);
            toPage.Items.Add(item);
        }
        item.Row = row;
        item.Col = col;
        return true;
    }

    /// <summary>複製を (row, col) に置く。空いていなければ null。ショートカットキーは複製しない（重複を避ける）。</summary>
    public static LauncherItem? Duplicate(LauncherItem item, Page toPage, int row, int col)
    {
        if (ItemAt(toPage, row, col) is not null) return null;
        var copy = Clone(item, keepHotkey: false);
        copy.Row = row;
        copy.Col = col;
        toPage.Items.Add(copy);
        return copy;
    }

    /// <summary>新しい Id で写しを作る（位置はそのまま。ページへは追加しない）。</summary>
    public static LauncherItem Clone(LauncherItem item, bool keepHotkey) => new()
    {
        Row = item.Row,
        Col = item.Col,
        Kind = item.Kind,
        Target = item.Target,
        Name = item.Name,
        Args = item.Args,
        WorkingDir = item.WorkingDir,
        IconOverride = item.IconOverride,
        LaunchMode = item.LaunchMode,
        RunAsAdmin = item.RunAsAdmin,
        Hotkey = keepHotkey ? item.Hotkey : null,
        LinkPath = item.LinkPath,
        Color = item.Color,
    };

    public static bool Remove(Page page, LauncherItem item) => page.Items.Remove(item);

    /// <summary>読み順（左上 → 右 → 次の行）で最初の空きセル。無ければ null。</summary>
    public static (int Row, int Col)? FirstEmpty(Page page) =>
        FindEmptySlots(page, 0, 0, 1) is [var cell] ? cell : null;

    // ---------------- 行列数の変更と範囲外のアイテム（SPEC §6.5） ----------------

    private static bool Within(LauncherItem i, int rows, int cols) => i.Row >= 0 && i.Col >= 0 && i.Row < rows && i.Col < cols;

    /// <summary>行列数を (rows, cols) にしたとき範囲の外に出るアイテム数。</summary>
    public static int CountOutOfRange(Page page, int rows, int cols) => page.Items.Count(i => !Within(i, rows, cols));

    /// <summary>(rows, cols) の範囲の中の空きセル数（範囲内のアイテムが占めていないセル）。</summary>
    public static int CountEmptyWithin(Page page, int rows, int cols)
    {
        var occupied = page.Items.Where(i => Within(i, rows, cols)).Select(i => (i.Row, i.Col)).ToHashSet();
        return rows * cols - occupied.Count;
    }

    /// <summary>行列数を変えても全アイテムが入りきるか。overflow = 範囲外に出て詰め直しが要る件数。</summary>
    public static bool CanResize(Page page, int rows, int cols, out int overflow)
    {
        overflow = CountOutOfRange(page, rows, cols);
        return overflow <= CountEmptyWithin(page, rows, cols);
    }

    /// <summary>
    /// 範囲外のアイテム（と同じセルに重なったアイテム）を読み順で空きへ移す。allowGrow なら空きが足りないとき行を足す（上限 12）。
    /// それでも入らなければ残す（消さない）。移した件数を返す。
    /// </summary>
    public static int ReflowOutOfRange(Page page, bool allowGrow)
    {
        var seen = new HashSet<(int, int)>();
        var homeless = new List<LauncherItem>();
        foreach (var item in page.Items)
        {
            if (!Within(item, page.Rows, page.Cols) || !seen.Add((item.Row, item.Col))) homeless.Add(item);
        }
        if (homeless.Count == 0) return 0;

        int moved = 0;
        foreach (var item in homeless)
        {
            var cell = FirstEmptyExcept(page, seen);
            while (cell is null && allowGrow && page.Rows < Page.MaxGrid)
            {
                page.Rows++;
                cell = FirstEmptyExcept(page, seen);
            }
            if (cell is not { } c) continue; // 入らない: データには残したまま表示しない
            item.Row = c.Row;
            item.Col = c.Col;
            seen.Add(c);
            moved++;
        }
        return moved;
    }

    private static (int Row, int Col)? FirstEmptyExcept(Page page, HashSet<(int, int)> taken)
    {
        for (int r = 0; r < page.Rows; r++)
            for (int c = 0; c < page.Cols; c++)
                if (!taken.Contains((r, c))) return (r, c);
        return null;
    }
}
