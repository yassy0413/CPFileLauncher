namespace FileLauncher.Core.Items;

/// <summary>盤面内ドラッグの離した場所の種類。</summary>
public enum DragTargetKind
{
    /// <summary>盤面の中だがスロットでもタブでもない（ヘッダーの空き等）。</summary>
    None,
    Slot,
    Tab,

    /// <summary>盤面（ウィンドウ）の外。削除候補。</summary>
    Outside,
}

/// <summary>ドラッグの結果どうするか。</summary>
public enum DragOutcome
{
    Cancel,
    Move,
    Swap,
    Duplicate,
    MoveToPage,
    DuplicateToPage,
    Delete,
}

/// <summary>盤面内ドラッグの判定（SPEC §6.2。OS・UI に依存しない純粋な規則）。</summary>
public static class BoardDragRules
{
    /// <summary>これだけ動かしたらドラッグを始める（Windows の SM_CXDRAG 既定値相当）。</summary>
    public const double ThresholdPx = 4;

    /// <summary>ページタブの上でこれだけ止まったらそのページへ切り替える。</summary>
    public const int TabHoverSwitchMs = 500;

    /// <summary>グリッド座標 (x, y) のセル。範囲外・負は null。</summary>
    public static (int Row, int Col)? CellAt(double x, double y, double cellW, double cellH, int rows, int cols)
    {
        if (x < 0 || y < 0 || cellW <= 0 || cellH <= 0) return null;
        int col = (int)(x / cellW), row = (int)(y / cellH);
        return row < rows && col < cols ? (row, col) : null;
    }

    /// <param name="targetOccupied">離したスロットに（ドラッグ中のもの以外の）アイテムがあるか。</param>
    /// <param name="sameCell">ドラッグ元と同じページの同じセルか。</param>
    /// <param name="copy">離した時点で複製の修飾キー（Win Ctrl / Mac ⌥）が押されているか。</param>
    public static DragOutcome Classify(DragTargetKind target, bool targetOccupied, bool sameCell, bool copy) => target switch
    {
        DragTargetKind.Slot when sameCell => DragOutcome.Cancel,
        DragTargetKind.Slot when copy => targetOccupied ? DragOutcome.Cancel : DragOutcome.Duplicate,
        DragTargetKind.Slot => targetOccupied ? DragOutcome.Swap : DragOutcome.Move,
        DragTargetKind.Tab => copy ? DragOutcome.DuplicateToPage : DragOutcome.MoveToPage,
        DragTargetKind.Outside => copy ? DragOutcome.Cancel : DragOutcome.Delete,
        _ => DragOutcome.Cancel,
    };
}
