using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Presenters;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using InputModifiers = Avalonia.Input.KeyModifiers;

namespace FileLauncher.App;

/// <summary>盤面へのドロップ。Cell が null なら盤面のスロット外（ヘッダー等）、TargetItem があれば Drop-to-Open。</summary>
internal sealed record DropRequest(
    IReadOnlyList<string> Paths, string? Text, (int Row, int Col)? Cell, LauncherItem? TargetItem, bool Move);

/// <summary>
/// 盤面ウィンドウ（SPEC §3.1）。ポップアップは Close せず Hide/Show で再利用する（表示遅延 100ms 以内のため）。
/// 盤面の中身は Render で組み立てる（データが変わったときとページ切替時のみ。表示のたびには作り直さない）。
/// </summary>
public partial class BoardWindow : Window
{
    private const int FramePadding = 6;
    private const int HeaderHeight = 34;
    private const int StatusBarHeight = 19; // ステータス行 17 + 上の余白 2
    private const int LabelHeight = 18;

    private Board _board = Board.CreateDefault();
    private AppearanceSettings _appearance = new();
    private int _pageIndex;
    private int _cellW, _cellH;
    private double _renderedScaling;
    private Size _boardSize;
    private EffectSpec _showEffect = EffectSpec.None;
    private EffectSpec _hideEffect = EffectSpec.None;
    private EffectSpec _hoverEffect = EffectSpec.None;
    private EffectSpec _pageEffect = EffectSpec.None;
    private EffectSpec _tabEffect = EffectSpec.None;
    private EffectSpec _pressEffect = EffectSpec.None;
    private EffectSpec _launchEffect = EffectSpec.None;
    private EffectSpec _labelEffect = EffectSpec.None;
    private EffectSpec _iconEffect = EffectSpec.None;
    private EffectSpec _dropEffect = EffectSpec.None;
    private readonly Dictionary<LauncherItem, Control> _iconHosts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(int Row, int Col), Button> _slots = new();
    private Button? _dropHighlight;
    private CancellationTokenSource? _pageCts;

    /// <summary>ヘッダーのドラッグで動かせるか（常駐の「位置をロック」で false）。</summary>
    public bool AllowMove { get; set; } = true;

    /// <summary>true にするまで Close は Hide に置き換える（終了時のみ true）。</summary>
    public bool AllowClose { get; set; }

    internal IconService? Icons { get; set; }

    /// <summary>アイテムの起動。bool は「新しいウィンドウで開く」（Windows の Ctrl+クリック / Ctrl+Enter。SPEC §5.2）。</summary>
    public event Action<LauncherItem, bool>? ItemInvoked;
    public event Action? EscapePressed;

    /// <summary>盤面表示中の Ctrl+, / ⌘,（SPEC §7 設定画面の入口）。</summary>
    public event Action? SettingsRequested;
    internal event Action<DropRequest>? Dropped;

    // 枠の部品（Themes/FrameChrome）の各部。Root = 余白込みの枠全体、Frame = 面
    private FrameChrome Root => Chrome;
    private Border Frame => Chrome.Frame;
    /// <summary>グリッチ演出の静止画層（spec/EFFECTS.md「glitch の詳細」。普段は非表示）。</summary>
    private readonly Canvas GlitchLayer = new() { Name = "GlitchLayer", IsVisible = false, IsHitTestVisible = false, ClipToBounds = false };
    /// <summary>盤面内ドラッグのゴースト（M4。最上段）。</summary>
    private readonly Canvas DragLayer = new() { Name = "DragLayer", IsVisible = false, IsHitTestVisible = false, ClipToBounds = false };

    /// <summary>
    /// 背景画像（SPEC §3.7）。image が null なら面の色だけ。画像があるときは面の色の代わりに画像（不透明度 imageOpacity）を敷き、
    /// その上に面の色（FlBoardBackground、テーマ・基調色に追従）を覆いとして重ねる。
    /// 描き直し（Render）は要らない。Hide/Show をまたいでブラシは張ったまま。
    /// </summary>
    public void ApplyBackground(BackgroundSettings bg, Avalonia.Media.Imaging.Bitmap? image)
    {
        if (image is null)
        {
            Backdrop.Background = null;
            Overlay.IsVisible = false;
            Chrome.SetSurfaceVisible(true);
            return;
        }
        // 画像があるときは地の色を描かない。暗さは覆いだけで決まり、画像の不透明度を下げるとデスクトップが透ける
        Chrome.SetSurfaceVisible(false);
        Backdrop.Opacity = bg.ImageOpacity / 100.0;
        var brush = new ImageBrush(image);
        switch (bg.Fit)
        {
            case BackgroundFit.Fit:
                brush.Stretch = Stretch.Uniform;
                break;
            case BackgroundFit.Stretch:
                brush.Stretch = Stretch.Fill;
                break;
            case BackgroundFit.Tile:
                // 原寸で並べる（DestinationRect を絶対単位の原寸にしないと 1 枚しか描かれない）
                double scale = image.Dpi.X > 0 ? image.Dpi.X / 96 : 1;
                brush.Stretch = Stretch.None;
                brush.TileMode = TileMode.Tile;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                brush.DestinationRect = new RelativeRect(0, 0, image.PixelSize.Width / scale, image.PixelSize.Height / scale, RelativeUnit.Absolute);
                break;
            case BackgroundFit.Center:
                brush.Stretch = Stretch.None;
                break;
            default:
                brush.Stretch = Stretch.UniformToFill;
                break;
        }
        brush.AlignmentX = bg.Fit == BackgroundFit.Tile ? AlignmentX.Left : AlignmentX.Center;
        brush.AlignmentY = bg.Fit == BackgroundFit.Tile ? AlignmentY.Top : AlignmentY.Center;
        RenderOptions.SetBitmapInterpolationMode(Backdrop, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
        Backdrop.Background = brush;
        Overlay.Opacity = bg.Overlay / 100.0;
        Overlay.IsVisible = bg.Overlay > 0;
    }

    public BoardWindow()
    {
        InitializeComponent();
        // 常時の演出（明滅 → 光の玉）はグリッチの静止画・ドラッグのゴーストより下（spec/EFFECTS.md「常時の演出の詳細」）
        _pulseLayer = new PulseLayer();
        _orbLayer = new OrbLayer();
        _ambientRoot = new Grid { IsHitTestVisible = false, IsVisible = false, Opacity = 0, Children = { _pulseLayer, _orbLayer } };
        _ambient = new AmbientAnimator(this, _orbLayer, _pulseLayer);
        // 明滅の画像は大きさ・テーマ（基調色の切替もテーマの入れ直しで来る）が変わったら焼き直す
        _pulseLayer.SizeChanged += (_, _) => { if (_ambient.IsRunning) _pulseLayer.Refresh(); };
        ActualThemeVariantChanged += (_, _) =>
        {
            if (_ambient.IsRunning) Avalonia.Threading.Dispatcher.UIThread.Post(_pulseLayer.Refresh, Avalonia.Threading.DispatcherPriority.Loaded);
        };
        Chrome.Overlays.Children.Add(_ambientRoot);
        Chrome.Overlays.Children.Add(GlitchLayer);
        Chrome.Overlays.Children.Add(DragLayer);

        Header.PointerPressed += (_, e) =>
        {
            if (AllowMove && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _dragging)
            {
                CancelDrag();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                EscapePressed?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.OemComma && e.KeyModifiers == (OperatingSystem.IsMacOS() ? InputModifiers.Meta : InputModifiers.Control))
            {
                SettingsRequested?.Invoke();
                e.Handled = true;
            }
        };

        // Explorer / ブラウザからの D&D（SPEC §6.1 登録、§5.3 Drop-to-Open）
        DragDrop.SetAllowDrop(Frame, true);
        Frame.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Frame.AddHandler(DragDrop.DropEvent, OnDrop);
        Frame.AddHandler(DragDrop.DragLeaveEvent, (_, _) => HighlightDropTarget(null));
        AttachBackgroundInput();
        AttachKeyboard();
        AttachDragInput();
        AttachMoveInput();
        AttachPageKeys();
    }

    public int PageIndex => _pageIndex;

    public void Render(Board board, AppearanceSettings appearance, int pageIndex)
    {
        _board = board;
        _appearance = appearance;
        _pageIndex = Math.Clamp(pageIndex, 0, board.Pages.Count - 1);
        _renderedScaling = RenderScaling;
        ApplyOpacity(appearance.Opacity);

        RenderTabs();
        RenderPage();
    }

    /// <summary>盤面の大きさを描画時の値に戻す（OS に縮められたとき用。中身は作り直さない）。</summary>
    public void RestoreSize()
    {
        Width = _boardSize.Width;
        Height = _boardSize.Height;
    }

    /// <summary>不透明度設定（SPEC §3.1）。演出の透明度（Frame）とは別の層（ウィンドウ）に掛ける。</summary>
    public void ApplyOpacity(int percent) => Opacity = percent / 100.0;

    /// <summary>演出の設定を反映する（spec/EFFECTS.md）。ホバーの色変化はスロットを作るときに付けるので描き直す。</summary>
    public void ApplyEffects(AppearanceSettings appearance)
    {
        _showEffect = EffectCatalog.Resolve(appearance, EffectCatalog.BoardShow);
        _hideEffect = EffectCatalog.Resolve(appearance, EffectCatalog.BoardHide);
        _hoverEffect = EffectCatalog.Resolve(appearance, EffectCatalog.ItemHover);
        _pageEffect = EffectCatalog.Resolve(appearance, EffectCatalog.PageSwitch);
        _tabEffect = EffectCatalog.Resolve(appearance, EffectCatalog.TabHighlight);
        _pressEffect = EffectCatalog.Resolve(appearance, EffectCatalog.ItemPress);
        _launchEffect = EffectCatalog.Resolve(appearance, EffectCatalog.ItemLaunch);
        _labelEffect = EffectCatalog.Resolve(appearance, EffectCatalog.LabelHover);
        _iconEffect = EffectCatalog.Resolve(appearance, EffectCatalog.IconLoad);
        _dropEffect = EffectCatalog.Resolve(appearance, EffectCatalog.DropTarget);
        _orbLayer.Spec = EffectCatalog.Resolve(appearance, EffectCatalog.FrameOrb);
        _ambient.PulseSpec = EffectCatalog.Resolve(appearance, EffectCatalog.GlowPulse);
        _pulseLayer.IsVisible = _ambient.PulseSpec.IsActive;
        UpdateAmbient();
        if (_board.Pages.Count > 0 && SlotGrid.Children.Count > 0)
        {
            RenderTabs();
            RenderPage();
        }
    }

    public bool HasShowEffect => _showEffect.IsActive;

    /// <summary>表示演出の開始前の状態にする（Show() の前に呼ぶ。最初のフレームから演出が始まるように）。</summary>
    public void PrepareShow()
    {
        GlitchPlayer.Finish(Frame, GlitchLayer);
        Root.Transitions = null;
        if (_showEffect.Kind == EffectKind.Glitch && _showEffect.IsActive)
        {
            // グリッチは盤面の静止画を撮るので Frame は描いておき、全体（Root）を隠しておく
            EffectRunner.Reset(Frame, visible: true);
            Root.Opacity = 0;
        }
        else if (_showEffect.IsActive)
        {
            Root.Opacity = 1;
            Frame.Opacity = 0;
        }
        else
        {
            Root.Opacity = 1;
            EffectRunner.Reset(Frame, visible: true);
        }
    }

    private readonly GlitchPlayer _glitch = new();

    /// <summary>グリッチの代わりに使う演出（静止画が撮れないとき。spec/EFFECTS.md glitch の詳細 7）。</summary>
    private static readonly EffectSpec GlitchFallback = new() { Kind = EffectKind.Fade, DurationMs = 100, Easing = EasingKind.EaseOut };

    public Task PlayShowAsync(CancellationToken ct) => DuringTransition(PlayShowCoreAsync(ct));

    private Task PlayShowCoreAsync(CancellationToken ct)
    {
        if (_showEffect.Kind == EffectKind.Glitch && _showEffect.IsActive)
        {
            var task = _glitch.Start(Root, Frame, GlitchLayer, _showEffect, Token("FlAccent"), Token("FlAccent2"), ct);
            if (task is not null)
            {
                AppLog.Info($"glitch: スナップショット {_glitch.LastSnapshotMs:F1} ms");
                return task;
            }
            AppLog.Info("glitch: 静止画を撮れないので fade で代替");
            Root.Opacity = 1;
            return EffectRunner.ShowAsync(Frame, GlitchFallback, ct);
        }
        return EffectRunner.ShowAsync(Frame, _showEffect, ct);
    }

    public Task PlayHideAsync(CancellationToken ct) => DuringTransition(PlayHideCoreAsync(ct));

    private Task PlayHideCoreAsync(CancellationToken ct)
    {
        // 表示のグリッチ中に閉じられたら、まず静止画層を片付ける（二重に見えないように）
        GlitchPlayer.Finish(Frame, GlitchLayer);
        Root.Transitions = null;
        Root.Opacity = 1;
        if (_hideEffect.Kind == EffectKind.Glitch && _hideEffect.IsActive)
        {
            var task = _glitch.Start(Root, Frame, GlitchLayer, _hideEffect, Token("FlAccent"), Token("FlAccent2"), ct, appearing: false);
            if (task is not null) return task;
            return EffectRunner.HideAsync(Frame, GlitchFallback with { Easing = EasingKind.EaseIn }, ct);
        }
        return EffectRunner.HideAsync(Frame, _hideEffect, ct);
    }

    /// <summary>今のテーマ・設定で表示 / 非表示にグリッチを使うか（常駐モードの演出に流用する）。</summary>
    public bool ShowUsesGlitch => _showEffect.Kind == EffectKind.Glitch && _showEffect.IsActive;
    public bool HideUsesGlitch => _hideEffect.Kind == EffectKind.Glitch && _hideEffect.IsActive;

    /// <summary>
    /// 表示中の盤面に一瞬グリッチを掛ける（常駐の前面化・自動で隠す。フェードは任意）。グリッチを使わない設定なら何もしない。
    /// </summary>
    public Task PlayGlitchAsync(bool appearing, bool fade, CancellationToken ct)
    {
        var spec = appearing ? _showEffect : _hideEffect;
        if (spec.Kind != EffectKind.Glitch || !spec.IsActive) return Task.CompletedTask;
        GlitchPlayer.Finish(Frame, GlitchLayer);
        return DuringTransition(_glitch.Start(Root, Frame, GlitchLayer, spec, Token("FlAccent"), Token("FlAccent2"), ct, appearing, fade)
            ?? Task.CompletedTask);
    }

    // ---------------- 常時の演出（光の玉・発光の明滅。spec/EFFECTS.md「常時の演出の詳細」） ----------------

    private readonly PulseLayer _pulseLayer;
    private readonly OrbLayer _orbLayer;
    private readonly Grid _ambientRoot;
    private readonly AmbientAnimator _ambient;
    private int _transitions; // 表示・非表示・グリッチの演出中（その間は止める）
    private bool _ambientSuspended;

    /// <summary>常駐の自動収納中など、外から止めたいとき（ResidentController が立てる）。</summary>
    public bool AmbientSuspended
    {
        get => _ambientSuspended;
        set { _ambientSuspended = value; UpdateAmbient(); }
    }

    /// <summary>OS の「視差効果を減らす」/「アニメーション効果 OFF」（App が Platform から渡す）。true の間は常時の演出を止める。</summary>
    public Func<bool>? ReducedMotion { get; set; }

    /// <summary>常時の演出が動いているか（テスト用）。</summary>
    internal bool AmbientRunning => _ambient.IsRunning;
    internal AmbientAnimator Ambient => _ambient;
    internal OrbLayer Orbs => _orbLayer;
    internal PulseLayer PulseLayer => _pulseLayer;

    private async Task DuringTransition(Task transition)
    {
        _transitions++;
        UpdateAmbient();
        try { await transition; }
        finally
        {
            _transitions--;
            UpdateAmbient();
        }
    }

    /// <summary>動く条件（種類・アニメーション・見えている・収納中でない・演出中でない・OS 設定）を見て、出す / 止める。</summary>
    private void UpdateAmbient()
    {
        if (_ambient is null) return; // 構築中（InitializeComponent の途中で IsVisible が変わる）
        if (_appearance is not null) UpdateClock(); // 時刻のタイマーも同じ条件（見えている・収納中でない）で動かす
        bool any = _orbLayer.Spec.IsActive || _ambient.PulseSpec.IsActive;
        bool run = any && IsVisible && !_ambientSuspended && _transitions == 0 && ReducedMotion?.Invoke() != true;
        if (run == _ambient.IsRunning) return;
        if (run)
        {
            _ambientRoot.IsVisible = true;
            if (_ambient.PulseSpec.IsActive)
                Avalonia.Threading.Dispatcher.UIThread.Post(_pulseLayer.Refresh, Avalonia.Threading.DispatcherPriority.Loaded); // 大きさが決まってから焼く
            _ambientRoot.Transitions = [new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) }];
            _ambientRoot.Opacity = 1;
            _ambient.Start();
        }
        else
        {
            _ambient.Stop();
            _ambientRoot.Transitions = null;
            _ambientRoot.Opacity = 0;
            _ambientRoot.IsVisible = false;
        }
    }

    /// <summary>演出を打ち切ったとき用: 表示状態に戻す（グリッチの静止画層も片付ける）。</summary>
    public void ResetFrame()
    {
        GlitchPlayer.Finish(Frame, GlitchLayer);
        Root.Transitions = null;
        Root.Opacity = 1;
        EffectRunner.Reset(Frame, visible: true);
        UpdateAmbient();
    }

    private IBrush Token(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Cyan;

    /// <summary>表示先モニタの拡大率が前回描画時と違えば、そのピクセル数でアイコンを取り直す。</summary>
    public void EnsureScaling()
    {
        if (Math.Abs(RenderScaling - _renderedScaling) > 0.01) Render(_board, _appearance, _pageIndex);
    }

    private void RenderTabs()
    {
        TabsPanel.Children.Clear();
        for (int i = 0; i < _board.Pages.Count; i++)
        {
            int index = i;
            var tab = new ToggleButton
            {
                // 選択中のタブの文字は強く、他は弱く光らせる（サイバーパンクのみ）
                Content = Themed.Glow(new TextBlock { Text = _board.Pages[i].Name }, strong: i == _pageIndex),
                IsChecked = i == _pageIndex,
                Classes = { "tab" },
            };
            tab.Click += (_, _) => SwitchPage(index);
            AttachPresenterTransitions(tab, _tabEffect);
            if (_tabEffect.Kind == EffectKind.Glow) tab.Classes.Add("glow");
            AttachTabInput(tab, index);
            TabsPanel.Children.Add(tab);
        }
        // ページが多くてタブがはみ出すとき、選択中のタブが見える位置までスクロールする（2026-10-04 ユーザー要望）。
        // タブは作り直したばかりで大きさが決まっていないので、レイアウトの後で
        Avalonia.Threading.Dispatcher.UIThread.Post(ScrollSelectedTabIntoView, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 隠れている間に描き直したときは大きさが決まらずスクロールできないので、出たときにもう一度
        if (change.Property == IsVisibleProperty && IsVisible)
            Avalonia.Threading.Dispatcher.UIThread.Post(ScrollSelectedTabIntoView, Avalonia.Threading.DispatcherPriority.Loaded);
        if (change.Property == IsVisibleProperty) UpdateAmbient();
    }

    /// <summary>選択中のタブがヘッダーの見える範囲に入るように横スクロールする（隣のタブが少し見える余白付き）。</summary>
    private void ScrollSelectedTabIntoView()
    {
        if (TabsPanel.Parent is not ScrollViewer viewer || _pageIndex >= TabsPanel.Children.Count) return;
        var tab = TabsPanel.Children[_pageIndex];
        const double Peek = 24;
        double left = tab.Bounds.X, right = tab.Bounds.Right;
        double viewport = viewer.Viewport.Width, offset = viewer.Offset.X;
        if (viewport <= 0) return;
        if (left - Peek < offset) offset = Math.Max(0, left - Peek);
        else if (right + Peek > offset + viewport) offset = right + Peek - viewport;
        else return;
        viewer.Offset = new Vector(Math.Min(offset, Math.Max(0, viewer.Extent.Width - viewport)), viewer.Offset.Y);
    }

    /// <summary>ページ切替（演出 pageSwitch。slide は切替方向から入ってくる）。</summary>
    private void SwitchPage(int index)
    {
        int direction = Math.Sign(index - _pageIndex);
        Render(_board, _appearance, index);
        _pageCts?.Cancel();
        _pageCts = new CancellationTokenSource();
        if (direction != 0) _ = EffectRunner.PageInAsync(SlotGrid, _pageEffect, direction, _pageCts.Token);
    }

    /// <summary>起動のフィードバック（演出 itemLaunch）。起動に成功したときに呼ばれる。</summary>
    public void PlayLaunch(LauncherItem item)
    {
        if (_iconHosts.TryGetValue(item, out var host)) _ = EffectRunner.PulseAsync(host, _launchEffect);
    }

    private void RenderPage()
    {
        var page = _board.Pages[_pageIndex];
        int size = page.ButtonSize ?? _appearance.ButtonSize;
        bool label = _appearance.Label != LabelMode.None; // hover もラベルの場所を取っておく（出たときに盤面が動かないように）
        _iconHosts.Clear();
        _slots.Clear();
        _dropHighlight = null;
        _cellW = size + 28;
        _cellH = size + 16 + (label ? LabelHeight : 0);

        SlotGrid.Children.Clear();
        SlotGrid.RowDefinitions.Clear();
        SlotGrid.ColumnDefinitions.Clear();
        for (int r = 0; r < page.Rows; r++) SlotGrid.RowDefinitions.Add(new RowDefinition(_cellH, GridUnitType.Pixel));
        for (int c = 0; c < page.Cols; c++) SlotGrid.ColumnDefinitions.Add(new ColumnDefinition(_cellW, GridUnitType.Pixel));

        var byCell = page.Items
            .Where(i => i.Row < page.Rows && i.Col < page.Cols) // 範囲外アイテムの扱いは M4
            .GroupBy(i => (i.Row, i.Col))
            .ToDictionary(g => g.Key, g => g.First());

        for (int r = 0; r < page.Rows; r++)
        for (int c = 0; c < page.Cols; c++)
        {
            var slot = byCell.TryGetValue((r, c), out var item) ? ItemButton(item, size, label) : EmptySlot();
            _slots[(r, c)] = slot;
            AttachSlotInput(slot, item, (r, c));
            Grid.SetRow(slot, r);
            Grid.SetColumn(slot, c);
            SlotGrid.Children.Add(slot);
        }

        // サイズは明示する（表示前に位置計算するため。SizeToContent だと表示するまで大きさが決まらない）
        // ウィンドウ = 盤面 + 発光の余白（サイバーパンクは 18、他は 0。SPEC §3.6）
        double glow = Chrome.GetMargin() * 2;
        double border = this.TryFindResource("FlBoardBorderThickness", ActualThemeVariant, out var bt) && bt is Thickness t ? t.Left : 1;
        BackgroundGrid.IsVisible = _appearance.Hud.Grid && _appearance.Hud.GridOpacity > 0;
        BackgroundGrid.Opacity = _appearance.Hud.GridOpacity / 100.0;
        bool status = _appearance.Hud.StatusBar;
        StatusBar.IsVisible = status;
        UpdateStatus();
        _boardSize = new Size(page.Cols * _cellW + (FramePadding + border) * 2 + glow,
            page.Rows * _cellH + HeaderHeight + (status ? StatusBarHeight : 0) + (FramePadding + border) * 2 + glow);
        RestoreSize();
        UpdateSelection();
    }

    private Button ItemButton(LauncherItem item, int size, bool label)
    {
        bool missing = IsMissing(item);
        var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
        int px = (int)Math.Round(size * RenderScaling);
        var placeholder = Placeholder(item, size);
        image.Source = Icons?.Get(item, px, _appearance.ImageThumbnails, loaded =>
        {
            image.Source = loaded;
            placeholder.IsVisible = false; // 透明部分から頭文字が透けないように
            if (_iconEffect.IsActive)
            {
                // 演出 iconLoad: 仮表示から実アイコンへフェード
                image.Transitions = null;
                image.Opacity = 0;
                image.Transitions = EffectRunner.StateTransitions(_iconEffect, Visual.OpacityProperty);
                image.Opacity = 1;
            }
        });
        placeholder.IsVisible = image.Source is null;

        var iconHost = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };
        iconHost.Children.Add(placeholder);
        iconHost.Children.Add(image);
        if (missing)
        {
            // 存在しないパス: グレーアウト + 警告マーク（SPEC §5.1。削除はしない）
            iconHost.Opacity = 0.4;
            iconHost.Children.Add(new Border
            {
                Width = size * 0.4, Height = size * 0.4,
                CornerRadius = new CornerRadius(size * 0.2),
                [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FlMissingBadgeBackground"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = new TextBlock
                {
                    Text = "!", FontWeight = FontWeight.Bold, FontSize = size * 0.28,
                    [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FlMissingBadgeText"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        _iconHosts[item] = iconHost;
        var content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(iconHost);
        TextBlock? labelText = null;
        if (label)
        {
            labelText = new TextBlock
            {
                [!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FlLabelText"),
                Text = item.Name,
                FontSize = 11,
                MaxWidth = size + 20,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Children.Add(labelText);
        }

        // 中身は上寄せ・中央。色帯（あれば）はスロットの左端に縦いっぱい
        content.VerticalAlignment = VerticalAlignment.Top;
        var cell = new Grid();
        if (item.Color is { } color) cell.Children.Add(ColorBand(color));
        cell.Children.Add(content);
        if (item.Hotkey is { Length: > 0 } hotkey) cell.Children.Add(HotkeyBadge(hotkey));
        var button = new Button
        {
            Content = cell,
            Classes = { "slot" },
            Focusable = false, // キーボードのフォーカスは盤面が持つ（SPEC §6.6）
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        AttachHoverTransitions(button);

        if (labelText is not null && _appearance.Label == LabelMode.Hover)
        {
            // ラベル表示「マウスを乗せたとき」（演出 labelHover）
            labelText.Opacity = 0;
            labelText.Transitions = EffectRunner.StateTransitions(_labelEffect, Visual.OpacityProperty);
            button.PointerEntered += (_, _) => labelText.Opacity = 1;
            button.PointerExited += (_, _) => labelText.Opacity = 0;
        }

        // 押下で少し縮む（演出 itemPress）。Button が押下イベントを処理済みにするので handledEventsToo で受ける
        if (_pressEffect.IsActive)
        {
            content.RenderTransformOrigin = RelativePoint.Center;
            content.Transitions = EffectRunner.StateTransitions(_pressEffect, Visual.RenderTransformProperty);
            button.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) content.RenderTransform = EffectRunner.PressedTransform;
            }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            void Release() => content.RenderTransform = EffectRunner.NoTransform;
            button.AddHandler(PointerReleasedEvent, (_, _) => Release(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            button.PointerCaptureLost += (_, _) => Release();
            button.PointerExited += (_, _) => Release();
        }
        ToolTip.SetTip(button, missing ? Strings.FormatBoard_ItemMissing_Tooltip(item.Target) : $"{item.Name}\n{item.Target}");
        button.Click += (_, _) =>
        {
            bool newWindow = IsNewWindowModifier(TakeReleaseModifiers()); // 抑止したクリックでも控えは消す
            if (!ConsumeSuppressedClick()) ItemInvoked?.Invoke(item, newWindow);
        };
        return button;
    }

    /// <summary>
    /// アイテムの色帯（SPEC §3.6「アイテムの色帯」）。スロットの内側の左端に幅 3 px、上下 3 px 内側。
    /// サイバーパンクでは帯の色で発光する（FlItemBandGlowBlur。他のテーマは 0 = なし）。欠落アイテムでも薄くしない。
    /// </summary>
    private Control ColorBand(ItemColor color)
    {
        string key = AppTheme.ItemColorKey(color);
        var band = new Border
        {
            CornerRadius = new CornerRadius(1.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(-2, -1, 0, -1), // Button の Padding（4）の内側から枠寄せ
            IsHitTestVisible = false,
            [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(key),
            [!Layoutable.WidthProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FlItemBandWidth"),
            [!Visual.OpacityProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FlItemBandOpacity"),
        };
        // 発光の色は帯の色に合わせて作る（テーマを変えると Render し直されるので、ここで引いた値で足りる）
        if (this.TryFindResource("FlItemBandGlowBlur", ActualThemeVariant, out var blurObj) && blurObj is double blur && blur > 0
            && this.TryFindResource(key, ActualThemeVariant, out var brushObj) && brushObj is ISolidColorBrush brush)
        {
            var c = brush.Color;
            band.BoxShadow = new BoxShadows(new BoxShadow { Blur = blur, Color = Color.FromArgb(0x99, c.R, c.G, c.B) });
        }
        return band;
    }

    /// <summary>アイコン取得中の仮表示（頭文字）。取得できたら上に重ねた Image が覆う。</summary>
    private static Control Placeholder(LauncherItem item, int size) => new Border
    {
        CornerRadius = new CornerRadius(size / 6.0),
        Background = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)),
        Child = new TextBlock
        {
            Text = item.Name.Length > 0 ? item.Name[..1].ToUpperInvariant() : "?",
            FontSize = size * 0.45,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private static bool IsMissing(LauncherItem item)
    {
        if (item.Kind is ItemKind.Url or ItemKind.Command) return false;
        string path = LaunchArgs.ExpandPath(item.Target);
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false; // ネットワークは確認に時間がかかりうるので見ない
        return !File.Exists(path) && !Directory.Exists(path);
    }

    private Button EmptySlot()
    {
        var button = new Button { Classes = { "slot", "empty" }, Focusable = false, Content = new CornerTicks() };
        button.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        AttachHoverTransitions(button);
        return button;
    }

    /// <summary>
    /// ホバー強調・ドロップ先の色の変化に時間を付ける（演出 itemHover / dropTarget。背景は同じ部品なので、
    /// ホバーが「なし」ならドロップ先の時間を使う）。色は BoardWindow.axaml のスタイル。
    /// </summary>
    private void AttachHoverTransitions(Button button)
    {
        AttachPresenterTransitions(button, _hoverEffect.IsActive ? _hoverEffect : _dropEffect);
        if (_hoverEffect.Kind == EffectKind.Glow) button.Classes.Add("glow");      // 発光は BoardWindow.axaml のスタイル
        if (_dropEffect.Kind == EffectKind.Glow) button.Classes.Add("drop-glow");
    }

    private static void AttachPresenterTransitions(ContentControl control, EffectSpec spec)
    {
        if (!spec.IsActive) return;
        control.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<ContentPresenter>("PART_ContentPresenter") is not { } presenter) return;
            presenter.Transitions = EffectRunner.StateTransitions(spec,
                ContentPresenter.BackgroundProperty, ContentPresenter.BorderBrushProperty, ContentPresenter.BoxShadowProperty);
        };
    }

    /// <summary>D&D 中にドロップ先のスロットを強調する（演出 dropTarget）。null で解除。</summary>
    private void HighlightDropTarget((int Row, int Col)? cell)
    {
        var slot = cell is { } c ? _slots.GetValueOrDefault(c) : null;
        if (ReferenceEquals(slot, _dropHighlight)) return;
        _dropHighlight?.Classes.Remove("drop-target");
        _dropHighlight = slot;
        slot?.Classes.Add("drop-target");
    }

    // ---------------- D&D ----------------

    private (int Row, int Col)? CellAt(DragEventArgs e)
    {
        var page = _board.Pages[_pageIndex];
        var p = e.GetPosition(SlotGrid);
        if (p.X < 0 || p.Y < 0 || _cellW == 0 || _cellH == 0) return null;
        int col = (int)(p.X / _cellW), row = (int)(p.Y / _cellH);
        return row < page.Rows && col < page.Cols ? (row, col) : null;
    }

    private LauncherItem? ItemAtCell((int Row, int Col)? cell) =>
        cell is { } c ? BoardEditing.ItemAt(_board.Pages[_pageIndex], c.Row, c.Col) : null;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool hasData = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text);
        if (!hasData)
        {
            e.DragEffects = DragDropEffects.None;
            HighlightDropTarget(null);
            return;
        }
        var cell = CellAt(e);
        HighlightDropTarget(cell);
        var target = ItemAtCell(cell);
        bool move = target?.Kind == ItemKind.Folder && e.KeyModifiers.HasFlag(InputModifiers.Shift);
        e.DragEffects = move ? DragDropEffects.Move : target is null ? DragDropEffects.Link : DragDropEffects.Copy;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = new List<string>();
        if (e.DataTransfer.TryGetFiles() is { } files)
        {
            foreach (var f in files)
                if (f.TryGetLocalPath() is { } p) paths.Add(p);
        }
        string? text = paths.Count == 0 ? e.DataTransfer.TryGetText() : null;
        var cell = CellAt(e);
        HighlightDropTarget(null);
        e.Handled = true;
        Dropped?.Invoke(new DropRequest(paths, text, cell, ItemAtCell(cell), e.KeyModifiers.HasFlag(InputModifiers.Shift)));
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
