using FileLauncher.Core.Input;
using FileLauncher.Core.Model;

namespace FileLauncher.Core.Popup;

/// <summary>ポップアップ盤面の表示位置（SPEC §3.3）。すべて物理 px。</summary>
public static class PopupPlacement
{
    /// <summary>カーソル位置表示でカーソルが盤面上端からどれだけ下に来るか（タブの上あたり）。</summary>
    public const int CursorOffsetFromTop = 20;

    /// <param name="cursor">カーソル（トリガー）位置。</param>
    /// <param name="workAreaAt">指定座標を含む（無ければ最寄りの）モニタの作業領域（タスクバー除く）。</param>
    /// <param name="primaryWorkArea">「画面中央」用のプライマリモニタ作業領域。</param>
    public static ScreenPoint Compute(
        PopupPlacementSettings settings, ScreenPoint cursor, int width, int height,
        Func<ScreenPoint, ScreenRect> workAreaAt, ScreenRect primaryWorkArea)
    {
        switch (settings.Position)
        {
            case PopupPosition.LastPosition or PopupPosition.Fixed when settings.X.HasValue && settings.Y.HasValue:
                // 保存座標のモニタに収める（外したモニタの座標なら最寄りのモニタへ）
                var saved = new ScreenPoint(settings.X.Value, settings.Y.Value);
                return ClampInto(saved, width, height, workAreaAt(saved));
            case PopupPosition.ScreenCenter:
                var a = primaryWorkArea;
                return ClampInto(new ScreenPoint(a.X + (a.Width - width) / 2, a.Y + (a.Height - height) / 2), width, height, a);
            default: // Cursor（前回位置・固定座標が未設定の場合もここ）
                return ClampInto(new ScreenPoint(cursor.X - width / 2, cursor.Y - CursorOffsetFromTop), width, height, workAreaAt(cursor));
        }
    }

    /// <summary>はみ出さないよう作業領域内に収める。盤面の方が大きければ左上を合わせる。</summary>
    public static ScreenPoint ClampInto(ScreenPoint p, int width, int height, ScreenRect area) => new(
        Math.Clamp(p.X, area.X, Math.Max(area.X, area.Right - width)),
        Math.Clamp(p.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
}
