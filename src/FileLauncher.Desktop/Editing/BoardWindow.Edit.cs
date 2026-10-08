using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using InputModifiers = Avalonia.Input.KeyModifiers;

namespace FileLauncher.App;

/// <summary>盤面内ドラッグの結果（判定は BoardEditor が Core の BoardDragRules で行う）。</summary>
internal sealed record SlotDrop(int FromPage, LauncherItem Item, DragTargetKind Target, int? ToPage, (int Row, int Col)? Cell, bool Copy);

internal enum SlotContextKind
{
    Item,
    EmptySlot,
    Tab,
    Background,
}

/// <summary>右クリックの場所（Anchor はメニューを出す基準のコントロール）。</summary>
internal sealed record SlotContext(SlotContextKind Kind, LauncherItem? Item, (int Row, int Col)? Cell, int? PageIndex, Control Anchor);

/// <summary>
/// 盤面の編集の受け口（M4。SPEC §6.2 / §6.3）。BoardWindow はビューに徹し、データは触らずにイベントを出す。
/// 盤面内のドラッグはポインタ操作の自前実装（Avalonia の DragDrop は外部からの受け口だけに使う）:
/// 押して 4 px 動いたらドラッグ開始 → ゴーストが付いてくる → 離した場所を判定して ItemDropped。
/// </summary>
public partial class BoardWindow
{
    internal event Action<SlotDrop>? ItemDropped;
    internal event Action<SlotContext>? ContextMenuRequested;

    private LauncherItem? _pressItem;
    private Button? _pressButton;
    private Point _pressAt;
    private bool _dragging;
    private bool _suppressClick;
    private Border? _ghost;
    private Border? _ghostBadge;
    private int _dragFromPage;
    private bool _movingCapture; // ドラッグ開始でポインタの捕捉をボタン → Root へ移している最中
    private int _tabHoverIndex = -1;
    private CancellationTokenSource? _tabHoverCts;

    /// <summary>空きスロットのダブルクリック（新規登録）。</summary>
    internal event Action<(int Row, int Col)>? EmptySlotActivated;

    /// <summary>ページの設定を開いてほしい（タブのダブルクリック）。</summary>
    internal event Action<int>? PageSettingsRequested;

    /// <summary>ページ切替の設定（ホイール・Alt+数字）。App が渡す。</summary>
    internal Func<BoardSettings>? BoardOptions { get; set; }

    /// <summary>盤面内でアイテムをドラッグしている最中（ポップアップのマウス離れ・常駐の自動で隠すを止めるのに使う）。</summary>
    public bool IsDragging => _dragging;

    /// <summary>複製の修飾キー（Win Ctrl / Mac ⌥）。</summary>
    private static bool IsCopyModifier(InputModifiers m) =>
        OperatingSystem.IsMacOS() ? m.HasFlag(InputModifiers.Alt) : m.HasFlag(InputModifiers.Control);

    /// <summary>「新しいウィンドウで開く」の修飾キー（Win Ctrl / Mac ⌘。Mac の ⌃クリックは右クリックのまま。SPEC §6.2）。</summary>
    private static bool IsNewWindowModifier(InputModifiers m) =>
        OperatingSystem.IsMacOS() ? m.HasFlag(InputModifiers.Meta) : m.HasFlag(InputModifiers.Control);

    /// <summary>
    /// 直前のポインタ離上の修飾キー。Button.Click は修飾キーを持たず、Button の Click は同じ要素の instance handler より
    /// 先に発火するので、ウィンドウで Tunnel のうちに控えておく。
    /// </summary>
    private InputModifiers _releaseModifiers;

    /// <summary>
    /// 控えた修飾キーを 1 回だけ使う（使ったら消す）。ポインタの離上を伴わない Click（自動操作など）に、
    /// 前回のクリックの Ctrl が残って「新しいウィンドウ」になるのを防ぐ。
    /// </summary>
    private InputModifiers TakeReleaseModifiers()
    {
        var m = _releaseModifiers;
        _releaseModifiers = InputModifiers.None;
        return m;
    }

    /// <summary>スロット（アイテム / 空き）にドラッグと右クリックの受け口を付ける。RenderPage から呼ぶ。</summary>
    private void AttachSlotInput(Button slot, LauncherItem? item, (int Row, int Col) cell)
    {
        // Button が押下を処理済みにするので handledEventsToo で受ける
        slot.AddHandler(PointerPressedEvent, (_, e) => OnSlotPressed(slot, item, cell, e), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        slot.AddHandler(PointerMovedEvent, (_, e) => OnSlotMoved(e), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        slot.AddHandler(PointerReleasedEvent, (_, e) => OnSlotReleased(slot, item, cell, e), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        slot.PointerCaptureLost += (_, _) => { if (!_dragging) { _pressItem = null; _pressButton = null; } };
        if (item is null) slot.DoubleTapped += (_, _) => EmptySlotActivated?.Invoke(cell);
    }

    /// <summary>ページタブの右クリック（メニュー）とダブルクリック（ページの設定）。</summary>
    private void AttachTabInput(Control tab, int index)
    {
        tab.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right) return;
            ContextMenuRequested?.Invoke(new SlotContext(SlotContextKind.Tab, null, null, index, tab));
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        tab.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (OperatingSystem.IsMacOS() && e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(InputModifiers.Control))
            {
                ContextMenuRequested?.Invoke(new SlotContext(SlotContextKind.Tab, null, null, index, tab));
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        tab.DoubleTapped += (_, _) => PageSettingsRequested?.Invoke(index);
    }

    /// <summary>ドラッグ中のポインタは Root で受ける（ページを切り替えるとスロットのボタンは作り直されて消えるため）。</summary>
    private void AttachDragInput()
    {
        Root.AddHandler(PointerMovedEvent, (_, e) => { if (_dragging) OnDragMoved(e); }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        Root.AddHandler(PointerReleasedEvent, (_, e) => { if (_dragging) OnDragReleased(e); }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) => _releaseModifiers = e.KeyModifiers, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        Root.PointerCaptureLost += (_, _) => { if (_dragging && !_movingCapture) CancelDrag(); };
    }

    // ---------------- ページの切替（SPEC §6.5） ----------------

    public void SwitchPageBy(int delta)
    {
        int to = Math.Clamp(_pageIndex + delta, 0, _board.Pages.Count - 1); // 端では折り返さない
        if (to != _pageIndex) SwitchPage(to);
    }

    public void SwitchPageTo(int index)
    {
        if (index >= 0 && index < _board.Pages.Count && index != _pageIndex) SwitchPage(index);
    }

    /// <summary>Ctrl+Tab（Tab はフォーカス移動に先に取られるのでトンネルで先取り）、Alt+数字、ホイール。</summary>
    private void AttachPageKeys()
    {
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(InputModifiers.Control))
            {
                SwitchPageBy(e.KeyModifiers.HasFlag(InputModifiers.Shift) ? -1 : 1);
                e.Handled = true;
            }
            else if (e.KeyModifiers == InputModifiers.Alt && BoardOptions?.Invoke().AltNumberSwitchesPage != false
                     && DigitOf(e) is { } n and >= 1 and <= 9)
            {
                SwitchPageTo(n - 1);
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Frame.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (_dragging || BoardOptions?.Invoke().WheelSwitchesPage == false || e.Delta.Y == 0) return;
            SwitchPageBy(e.Delta.Y < 0 ? 1 : -1); // 下 = 次のページ
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>数字キー（Mac の ⌥ は文字が変わるので物理キーで見る）。</summary>
    private static int? DigitOf(KeyEventArgs e) => e.PhysicalKey switch
    {
        >= PhysicalKey.Digit1 and <= PhysicalKey.Digit9 => e.PhysicalKey - PhysicalKey.Digit1 + 1,
        _ => null,
    };

    /// <summary>盤面の背景の右クリック。ページタブの列の空き部分は、今のページのタブを右クリックしたのと同じページのメニュー。</summary>
    private void AttachBackgroundInput()
    {
        // タブの上の右クリックはタブ自身が先に処理する（Handled）。ここに来るのはタブの無い所だけ
        Header.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.Handled || e.InitialPressMouseButton != MouseButton.Right) return;
            RequestCurrentPageMenu();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble);
        // Mac の ⌃クリック（ウィンドウ移動のドラッグより先に見る）
        Header.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Handled || !OperatingSystem.IsMacOS() || !e.GetCurrentPoint(Header).Properties.IsLeftButtonPressed || !e.KeyModifiers.HasFlag(InputModifiers.Control)) return;
            RequestCurrentPageMenu();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble);

        Frame.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.Handled || e.InitialPressMouseButton != MouseButton.Right) return;
            ContextMenuRequested?.Invoke(new SlotContext(SlotContextKind.Background, null, null, _pageIndex, Frame));
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    private void RequestCurrentPageMenu()
    {
        Control anchor = _pageIndex < TabsPanel.Children.Count ? TabsPanel.Children[_pageIndex] : Header;
        ContextMenuRequested?.Invoke(new SlotContext(SlotContextKind.Tab, null, null, _pageIndex, anchor));
    }

    private void OnSlotPressed(Button slot, LauncherItem? item, (int Row, int Col) cell, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        // Mac の ⌃クリックは右クリック扱い
        if (OperatingSystem.IsMacOS() && props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(InputModifiers.Control))
        {
            RequestSlotMenu(slot, item, cell);
            e.Handled = true;
            return;
        }
        if (!props.IsLeftButtonPressed || item is null) return;
        _pressItem = item;
        _pressButton = slot;
        _pressAt = e.GetPosition(this);
    }

    private void OnSlotMoved(PointerEventArgs e)
    {
        if (_dragging || _pressItem is null || _pressButton is null) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressAt.X) < BoardDragRules.ThresholdPx && Math.Abs(p.Y - _pressAt.Y) < BoardDragRules.ThresholdPx) return;
        BeginDrag();
        _movingCapture = true;
        e.Pointer.Capture(Root);
        _movingCapture = false;
        OnDragMoved(e);
    }

    private void OnDragMoved(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        MoveGhost(p, IsCopyModifier(e.KeyModifiers));
        var (target, cell, page) = HitTest(p);
        HighlightDropTarget(target == DragTargetKind.Slot ? cell : null);
        HighlightTab(target == DragTargetKind.Tab ? page : null);

        // タブの上に 500 ms 留まったらそのページへ（そのままスロットへ落とせる。SPEC §6.2）。
        // マウスを止めていても切り替わるようタイマーで待つ（移動イベントだけで測ると、止めている間は切り替わらなかった）
        int hover = target == DragTargetKind.Tab && page is { } tabIndex && tabIndex != _pageIndex ? tabIndex : -1;
        if (hover == _tabHoverIndex) return;
        _tabHoverIndex = hover;
        _tabHoverCts?.Cancel();
        if (hover < 0) return;
        var cts = _tabHoverCts = new CancellationTokenSource();
        _ = SwitchAfterHoverAsync(hover, cts.Token);
    }

    private async Task SwitchAfterHoverAsync(int index, CancellationToken ct)
    {
        try { await Task.Delay(BoardDragRules.TabHoverSwitchMs, ct); }
        catch (OperationCanceledException) { return; }
        if (_dragging && _tabHoverIndex == index) SwitchPage(index);
    }

    private void OnDragReleased(PointerReleasedEventArgs e)
    {
        var dragged = _pressItem!;
        var (target, dropCell, toPage) = HitTest(e.GetPosition(this));
        bool copy = IsCopyModifier(e.KeyModifiers);
        int fromPage = _dragFromPage;
        e.Pointer.Capture(null);
        EndDrag();
        e.Handled = true;
        ItemDropped?.Invoke(new SlotDrop(fromPage, dragged, target, toPage, dropCell, copy));
    }

    /// <summary>ドラッグ中、離すとそのページへ移るタブを強調する。</summary>
    private void HighlightTab(int? index)
    {
        for (int i = 0; i < TabsPanel.Children.Count; i++)
        {
            if (TabsPanel.Children[i] is not Control tab) continue;
            if (i == index) tab.Classes.Add("drop-target");
            else tab.Classes.Remove("drop-target");
        }
    }

    private void OnSlotReleased(Button slot, LauncherItem? item, (int Row, int Col) cell, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if (!_dragging) RequestSlotMenu(slot, item, cell);
            e.Handled = true;
            return;
        }
        if (!_dragging)
        {
            _pressItem = null;
            _pressButton = null;
        }
    }

    private void RequestSlotMenu(Button slot, LauncherItem? item, (int Row, int Col) cell) =>
        ContextMenuRequested?.Invoke(new SlotContext(item is null ? SlotContextKind.EmptySlot : SlotContextKind.Item, item, cell, _pageIndex, slot));

    /// <summary>ドラッグの後に元のボタンへ来る Click を捨てる（元のセルで離すと Click が来る）。</summary>
    private bool ConsumeSuppressedClick()
    {
        if (!_suppressClick) return false;
        _suppressClick = false;
        return true;
    }

    /// <summary>離した場所: スロット → タブ → ウィンドウの外 → それ以外（SPEC §6.2 の優先順）。座標はウィンドウ基準。</summary>
    private (DragTargetKind Target, (int Row, int Col)? Cell, int? Page) HitTest(Point inWindow)
    {
        var page = _board.Pages[_pageIndex];
        if (this.TranslatePoint(inWindow, SlotGrid) is { } g
            && BoardDragRules.CellAt(g.X, g.Y, _cellW, _cellH, page.Rows, page.Cols) is { } cell)
            return (DragTargetKind.Slot, cell, _pageIndex);

        for (int i = 0; i < TabsPanel.Children.Count; i++)
        {
            if (TabsPanel.Children[i] is Control tab && this.TranslatePoint(inWindow, tab) is { } t
                && t.X >= 0 && t.Y >= 0 && t.X < tab.Bounds.Width && t.Y < tab.Bounds.Height)
                return (DragTargetKind.Tab, null, i);
        }

        // 発光の余白を含むウィンドウ全体を盤面とみなす（SPEC §3.6）
        if (inWindow.X < 0 || inWindow.Y < 0 || inWindow.X > Bounds.Width || inWindow.Y > Bounds.Height)
            return (DragTargetKind.Outside, null, null);
        return (DragTargetKind.None, null, null);
    }

    // ---------------- ゴースト ----------------

    private void BeginDrag()
    {
        _dragging = true;
        _suppressClick = true;
        _dragFromPage = _pageIndex;
        _tabHoverIndex = -1;
        if (_pressButton is not null) _pressButton.Opacity = 0.35;

        var item = _pressItem!;
        var image = _iconHosts.TryGetValue(item, out var host) ? host.GetVisualChildrenImage() : null;
        double size = host?.Bounds.Width is > 0 and var w ? w : 48;
        var content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(image is not null
            ? new Image { Source = image, Width = size, Height = size }
            : new TextBlock { Text = item.Name.Length > 0 ? item.Name[..1] : "?", FontSize = size * 0.45, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = item.Name, FontSize = 11, MaxWidth = size + 20, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });

        _ghostBadge = new Border
        {
            Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -6, 0), IsVisible = false,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("FlAccent2"),
            Child = new TextBlock { Text = "+", Foreground = Brushes.White, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        _ghost = new Border
        {
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            Opacity = 0.9,
            IsHitTestVisible = false,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("FlSurface"),
            [!Border.BorderBrushProperty] = new DynamicResourceExtension("FlDragGhostBorder"),
            [!Border.CornerRadiusProperty] = new DynamicResourceExtension("FlSlotCornerRadius"),
            Child = new Grid { Children = { content, _ghostBadge } },
        };
        DragLayer.Children.Add(_ghost);
        DragLayer.IsVisible = true;
    }

    private void MoveGhost(Point inWindow, bool copy)
    {
        if (_ghost is null) return;
        if (this.TranslatePoint(inWindow, Root) is not { } p) return; // 層（DragLayer）は Root と同じ位置・大きさ
        // ウィンドウの外ではゴーストを縁に留め、枠を警告色に（離すと削除）
        bool outside = inWindow.X < 0 || inWindow.Y < 0 || inWindow.X > Bounds.Width || inWindow.Y > Bounds.Height;
        // ドラッグ開始の直後はゴーストも層もまだレイアウトされていない（Bounds が 0）。
        // そのまま使うと最初の 1 回だけ左上寄せ・中心ずれで置かれ、次の移動で飛ぶ（2026-10-03 ユーザー報告の「初動のワープ」）。
        // ゴーストは自分で測り、層の大きさは常にレイアウト済みの枠（Root）から取る
        if (_ghost.DesiredSize == default) _ghost.Measure(Size.Infinity);
        double gw = _ghost.DesiredSize.Width, gh = _ghost.DesiredSize.Height;
        double lw = Root.Bounds.Width, lh = Root.Bounds.Height;
        double x = Math.Clamp(p.X - gw / 2, 0, Math.Max(0, lw - gw));
        double y = Math.Clamp(p.Y - gh / 2, 0, Math.Max(0, lh - gh));
        Canvas.SetLeft(_ghost, x);
        Canvas.SetTop(_ghost, y);
        _ghost[!Border.BorderBrushProperty] = new DynamicResourceExtension(outside && !copy ? "FlError" : "FlDragGhostBorder");
        if (_ghostBadge is not null) _ghostBadge.IsVisible = copy;
    }

    private void EndDrag()
    {
        _dragging = false;
        if (_pressButton is not null) _pressButton.Opacity = 1;
        _pressItem = null;
        _pressButton = null;
        _ghost = null;
        _ghostBadge = null;
        DragLayer.Children.Clear();
        DragLayer.IsVisible = false;
        HighlightDropTarget(null);
        HighlightTab(null);
        _tabHoverCts?.Cancel();
        _tabHoverIndex = -1;
    }

    /// <summary>ドラッグを取り消す（盤面を隠すとき・Esc・キャプチャが外れたとき）。ItemDropped は出さない。</summary>
    public void CancelDrag()
    {
        if (!_dragging && _pressItem is null) return;
        EndDrag();
        _suppressClick = false;
    }
}

internal static class BoardWindowVisualExtensions
{
    /// <summary>アイコン枠の中の Image のソース（取得済みならそのビットマップ）。</summary>
    public static IImage? GetVisualChildrenImage(this Control host) =>
        host is Panel panel ? panel.Children.OfType<Image>().FirstOrDefault()?.Source : null;
}
