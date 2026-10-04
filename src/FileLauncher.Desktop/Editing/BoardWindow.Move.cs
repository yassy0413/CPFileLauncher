using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileLauncher.Core.Items;
using InputModifiers = Avalonia.Input.KeyModifiers;

namespace FileLauncher.App;

/// <summary>
/// 盤面の移動（2026-10-03 ユーザー要望）: アイテム以外の所（タブ・空きスロット・枠の余白）を押したまま動かすとウィンドウが動く。
/// ページが多くてヘッダーに隙間が無いときでも動かせるように。クリック・ダブルクリック・右クリックを壊さないよう、
/// アイテムのドラッグと同じしきい値（4 px）を越えてから動かし始め、ポインタを Root に移してタブ・スロットのクリックを起こさない。
/// ヘッダーの隙間は従来どおり OS の移動（BeginMoveDrag）。
/// </summary>
public partial class BoardWindow
{
    private PixelPoint? _movePressScreen;   // 押した位置（画面座標）
    private PixelPoint _moveStartPosition;  // 押したときのウィンドウ位置
    private bool _windowMoving;

    /// <summary>ウィンドウを動かしている最中か（UI テスト用）。</summary>
    internal bool IsMovingWindow => _windowMoving;

    private void AttachMoveInput()
    {
        // スロット・タブの押下処理（Bubble）の後に来るので、アイテムを押したかどうかは _pressItem で分かる
        Root.AddHandler(PointerPressedEvent, (_, e) =>
        {
            _movePressScreen = null;
            var props = e.GetCurrentPoint(this).Properties;
            if (!AllowMove || !props.IsLeftButtonPressed || _dragging || _pressItem is not null) return;
            if (OperatingSystem.IsMacOS() && e.KeyModifiers.HasFlag(InputModifiers.Control)) return; // ⌃クリック = 右クリック
            if (e.ClickCount > 1) return; // ダブルクリック（空きスロットの登録・タブのページ設定）
            _movePressScreen = this.PointToScreen(e.GetPosition(this));
            _moveStartPosition = Position;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        Root.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_movePressScreen is not { } press || _dragging) return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { _movePressScreen = null; return; }
            var now = this.PointToScreen(e.GetPosition(this));
            double scale = RenderScaling > 0 ? RenderScaling : 1;
            if (!_windowMoving)
            {
                if (Math.Abs(now.X - press.X) / scale < BoardDragRules.ThresholdPx && Math.Abs(now.Y - press.Y) / scale < BoardDragRules.ThresholdPx) return;
                _windowMoving = true;
                e.Pointer.Capture(Root); // タブ・スロットのボタンからポインタを外す（離してもクリックにならない）
            }
            Position = new PixelPoint(_moveStartPosition.X + now.X - press.X, _moveStartPosition.Y + now.Y - press.Y);
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        Root.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_windowMoving)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
            }
            EndWindowMove();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        Root.PointerCaptureLost += (_, _) => { if (_windowMoving) EndWindowMove(); };
    }

    private void EndWindowMove()
    {
        _movePressScreen = null;
        _windowMoving = false;
    }
}
