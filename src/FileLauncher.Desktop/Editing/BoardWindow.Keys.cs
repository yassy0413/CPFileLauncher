using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using InputModifiers = Avalonia.Input.KeyModifiers;

namespace FileLauncher.App;

/// <summary>
/// 盤面のキーボード操作（M4 ステップ 4。SPEC §6.6）。キーボードのフォーカスは盤面ウィンドウ自身が持ち、
/// スロットのボタンはフォーカスを取らない。盤面は「選択セル」を 1 つ持つ（矢印で動かす。閉じたら消す）。
/// </summary>
public partial class BoardWindow
{
    internal event Action<LauncherItem>? EditRequested;
    internal event Action<LauncherItem>? DeleteRequested;
    internal event Action? UndoRequested;

    private (int Row, int Col)? _selected;

    /// <summary>キーボードの選択セル（無ければ null）。</summary>
    public (int Row, int Col)? SelectedCell => _selected;

    /// <summary>盤面を閉じたときに呼ぶ（次の表示は選択なしで始まる）。</summary>
    public void ClearSelection()
    {
        _selected = null;
        UpdateSelection();
    }

    private void AttachKeyboard()
    {
        // 矢印は Avalonia の方向キーによるフォーカス移動が先に取るので、トンネルで先取りする（Ctrl+Tab と同じ）
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Handled || _dragging) return;
            if (HandleItemHotkey(e) || HandleSelectionKey(e)) e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>修飾キーなしの英数字 = 表示中のページでそのキーを持つアイテムを起動（選択セルより優先）。</summary>
    private bool HandleItemHotkey(KeyEventArgs e)
    {
        if (e.KeyModifiers != InputModifiers.None) return false;
        string? key = e.PhysicalKey switch
        {
            >= PhysicalKey.A and <= PhysicalKey.Z => ((char)('A' + (e.PhysicalKey - PhysicalKey.A))).ToString(),
            >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9 => ((char)('0' + (e.PhysicalKey - PhysicalKey.Digit0))).ToString(),
            _ => null,
        };
        if (key is null || ItemHotkey.Find(_board.Pages[_pageIndex], key) is not { } item) return false;
        ItemInvoked?.Invoke(item, false);
        return true;
    }

    private bool HandleSelectionKey(KeyEventArgs e)
    {
        var page = _board.Pages[_pageIndex];
        var mods = e.KeyModifiers;
        bool command = mods == (OperatingSystem.IsMacOS() ? InputModifiers.Meta : InputModifiers.Control);
        switch (e.Key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down when mods == InputModifiers.None:
                MoveSelection(e.Key, page);
                return true;
            case Key.Enter when mods == InputModifiers.None && SelectedItem() is { } item:
                ItemInvoked?.Invoke(item, false);
                return true;
            case Key.Enter when mods == InputModifiers.Control && OperatingSystem.IsWindows() && SelectedItem() is { } item:
                ItemInvoked?.Invoke(item, true); // 新しいウィンドウで開く（SPEC §6.6）
                return true;
            case Key.Delete or Key.Back when mods == InputModifiers.None && SelectedItem() is { } item: // Mac の Delete は Back
                DeleteRequested?.Invoke(item);
                return true;
            case Key.F2 when mods == InputModifiers.None && SelectedItem() is { } item:
                EditRequested?.Invoke(item);
                return true;
            case Key.F10 when mods == InputModifiers.Shift:
            case Key.Apps:
                if (_selected is not { } cell || !_slots.TryGetValue(cell, out var slot)) return false;
                var selectedItem = BoardEditing.ItemAt(page, cell.Row, cell.Col);
                ContextMenuRequested?.Invoke(new SlotContext(selectedItem is null ? SlotContextKind.EmptySlot : SlotContextKind.Item, selectedItem, cell, _pageIndex, slot));
                return true;
            case Key.Z when command:
                UndoRequested?.Invoke();
                return true;
        }
        return false;
    }

    private LauncherItem? SelectedItem() =>
        _selected is { } c ? BoardEditing.ItemAt(_board.Pages[_pageIndex], c.Row, c.Col) : null;

    private void MoveSelection(Key key, Page page)
    {
        if (_selected is not { } c)
        {
            _selected = (0, 0);
        }
        else
        {
            // 端で止まる（折り返さない）
            _selected = key switch
            {
                Key.Left => (c.Row, Math.Max(0, c.Col - 1)),
                Key.Right => (c.Row, Math.Min(page.Cols - 1, c.Col + 1)),
                Key.Up => (Math.Max(0, c.Row - 1), c.Col),
                _ => (Math.Min(page.Rows - 1, c.Row + 1), c.Col),
            };
        }
        UpdateSelection();
    }

    /// <summary>選択セルのスロットに .selected を付け直す（描き直し・ページ切替のあとも同じ座標。範囲外なら端へ）。</summary>
    private void UpdateSelection()
    {
        if (_selected is { } c && _board.Pages.Count > 0)
        {
            var page = _board.Pages[_pageIndex];
            _selected = (Math.Min(c.Row, page.Rows - 1), Math.Min(c.Col, page.Cols - 1));
        }
        foreach (var (cell, slot) in _slots)
        {
            if (cell == _selected) slot.Classes.Add("selected");
            else slot.Classes.Remove("selected");
        }
    }

    /// <summary>ショートカットキーの小さな表示（スロットの右上）。</summary>
    private static Control HotkeyBadge(string key) => new TextBlock
    {
        Text = key,
        FontSize = 9,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Avalonia.Thickness(0, -2, -1, 0),
        IsHitTestVisible = false,
        [!TextBlock.ForegroundProperty] = new DynamicResourceExtension("FlTextMuted"),
    };
}
