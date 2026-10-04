using FileLauncher.Core.Input;

namespace FileLauncher.Core.Popup;

/// <summary>盤面が貼り付いている作業領域の辺（常駐モードの「自動で隠す」、SPEC §3.2）。</summary>
public enum ScreenEdge
{
    None,
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>常駐モードの位置計算（SPEC §3.2）。座標は画面座標（Win: 物理 px、Mac: pt）。</summary>
public static class ResidentLayout
{
    /// <summary>辺から何 px 以内なら「貼り付いている」とみなすか。</summary>
    public const int StickDistance = 8;

    /// <summary>収納したときに見えている帯の幅。</summary>
    public const int VisibleBand = 4;

    /// <summary>
    /// 盤面が作業領域のどの辺に貼り付いているか（いちばん近い辺。どれも 8 px より離れていれば None）。
    /// </summary>
    public static ScreenEdge StuckEdge(ScreenRect board, ScreenRect workArea)
    {
        var candidates = new (ScreenEdge Edge, int Distance)[]
        {
            (ScreenEdge.Left, Math.Abs(board.X - workArea.X)),
            (ScreenEdge.Right, Math.Abs(workArea.Right - board.Right)),
            (ScreenEdge.Top, Math.Abs(board.Y - workArea.Y)),
            (ScreenEdge.Bottom, Math.Abs(workArea.Bottom - board.Bottom)),
        };
        var nearest = candidates.MinBy(c => c.Distance);
        return nearest.Distance <= StickDistance ? nearest.Edge : ScreenEdge.None;
    }

    /// <summary>収納位置（辺の外へ出し、帯 4 px だけ作業領域に残す）。もう一方の軸はそのまま。</summary>
    public static ScreenPoint CollapsedPosition(ScreenRect board, ScreenEdge edge, ScreenRect workArea) => edge switch
    {
        ScreenEdge.Left => new ScreenPoint(workArea.X - board.Width + VisibleBand, board.Y),
        ScreenEdge.Right => new ScreenPoint(workArea.Right - VisibleBand, board.Y),
        ScreenEdge.Top => new ScreenPoint(board.X, workArea.Y - board.Height + VisibleBand),
        ScreenEdge.Bottom => new ScreenPoint(board.X, workArea.Bottom - VisibleBand),
        _ => new ScreenPoint(board.X, board.Y),
    };

    /// <summary>
    /// 起動時の位置。保存位置が無ければ主モニタの作業領域の中央、あれば（外したモニタの座標でも）画面内に寄せる。
    /// </summary>
    public static ScreenPoint InitialPosition(int? savedX, int? savedY, int width, int height,
        Func<ScreenPoint, ScreenRect> workAreaAt, ScreenRect primaryWorkArea)
    {
        if (savedX is { } x && savedY is { } y)
        {
            var saved = new ScreenPoint(x, y);
            return PopupPlacement.ClampInto(saved, width, height, workAreaAt(saved));
        }
        var a = primaryWorkArea;
        return PopupPlacement.ClampInto(new ScreenPoint(a.X + (a.Width - width) / 2, a.Y + (a.Height - height) / 2), width, height, a);
    }
}
