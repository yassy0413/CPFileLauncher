using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace PlatformSpike;

/// <summary>
/// ランチャー盤面の代わり。検証対象:
///   - 枚縁なし / タスクバー(Dock)非表示 / 最前面 / 半透明 が両 OS で効くか
///   - カーソル位置に表示し、画面外にはみ出さないよう補正できるか
///   - 非アクティブ化(Deactivated)で閉じられるか。閉じた後に元のアプリへフォーカスが戻るか
///   - Explorer / Finder からの D&D で複数パスが取れるか（.lnk / .app / フォルダ / URL テキスト）
///   - Hide/Show の繰り返しで表示遅延が体感 100ms 以内か
/// </summary>
public partial class PopupWindow : Window
{
    public bool CloseOnDeactivate { get; set; } = true;

    /// <summary>
    /// 外クリック・非アクティブ化で閉じた（＝フォーカスは既に別アプリへ移った）か。
    /// true のときは直前のアプリへフォーカスを戻してはいけない。表示のたびにリセット。
    /// </summary>
    public bool FocusMovedElsewhere { get; set; }

    private bool _selfHiding;

    public override void Hide()
    {
        _selfHiding = true;
        try { base.Hide(); }
        finally { _selfHiding = false; }
    }

    /// <summary>
    /// 盤面外でマウスボタンが押されている間 true（フックスレッドから書く）。
    /// この間は非アクティブ化で閉じない。Explorer 等でつかんだ瞬間に閉じると D&D 登録ができないため。
    /// </summary>
    public volatile bool HoldOpenWhilePressed;

    /// <summary>画面上の外接矩形（物理 px）。フック座標と比較する。</summary>
    public PixelRect ScreenBounds => new(Position, PixelSize.FromSize(ClientSize, RenderScaling));

    public event Action<string>? Log;
    public event Action? ItemLaunched;

    public PopupWindow()
    {
        InitializeComponent();

        for (int i = 1; i <= 12; i++)
        {
            var b = new Button
            {
                Content = $"Item {i}",
                Margin = new Thickness(3),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            int n = i;
            b.Click += (_, _) =>
            {
                Log?.Invoke($"item {n} launched");
                ItemLaunched?.Invoke();
            };
            BoardGrid.Children.Add(b);
        }

        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) => Hide();

        // --- D&D ---
        DragDrop.SetAllowDrop(DropZone, true);
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);

        // --- 閉じる条件 ---
        Deactivated += (_, _) =>
        {
            Log?.Invoke($"popup deactivated{(HoldOpenWhilePressed ? " (mouse pressed outside → keep open)" : "")}");
            // macOS は Hide() の途中で Deactivated が来る。自分で隠している最中は「他へ移った」とみなさない
            if (CloseOnDeactivate && IsVisible && !HoldOpenWhilePressed && !_selfHiding)
            {
                FocusMovedElsewhere = true;
                Hide();
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        };

        Opened += (_, _) => Log?.Invoke($"popup opened at {Position} scale={RenderScaling} transparency={ActualTransparencyLevel}");
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool ok = e.Data.Contains(DataFormats.Files) || e.Data.Contains(DataFormats.Text);
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var lines = new List<string>();

        if (e.Data.Contains(DataFormats.Files))
        {
            var items = e.Data.GetFiles();
            if (items is not null)
            {
                foreach (var item in items)
                {
                    string? path = item.TryGetLocalPath();
                    string kind = item is IStorageFolder ? "folder" : "file";
                    lines.Add($"{kind}: {path ?? item.Path.ToString()}");
                }
            }
        }
        if (e.Data.Contains(DataFormats.Text))
        {
            lines.Add($"text: {e.Data.GetText()}");
        }
        if (lines.Count == 0)
        {
            lines.Add("drop: formats=" + string.Join(",", e.Data.GetDataFormats()));
        }

        foreach (var l in lines) Log?.Invoke("DROP " + l);
        DropText.Text = $"{lines.Count} 件受信（ログ参照）";
        e.Handled = true;
    }

    /// <summary>画面座標 (物理px) を指定して表示。画面内に収まるよう補正する。</summary>
    public void ShowAt(PixelPoint anchor)
    {
        var screen = Screens.ScreenFromPoint(anchor) ?? Screens.Primary;
        var size = PixelSize.FromSize(ClientSize, screen?.Scaling ?? 1.0);

        int x = anchor.X - size.Width / 2;
        int y = anchor.Y - 20;

        if (screen is not null)
        {
            var wa = screen.WorkingArea;
            x = Math.Clamp(x, wa.X, Math.Max(wa.X, wa.Right - size.Width));
            y = Math.Clamp(y, wa.Y, Math.Max(wa.Y, wa.Bottom - size.Height));
        }

        Position = new PixelPoint(x, y);
        ShowAndActivate();
    }

    public void ShowCentered()
    {
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var size = PixelSize.FromSize(ClientSize, screen.Scaling);
            var wa = screen.WorkingArea;
            Position = new PixelPoint(wa.X + (wa.Width - size.Width) / 2, wa.Y + (wa.Height - size.Height) / 2);
        }
        ShowAndActivate();
    }

    private void ShowAndActivate()
    {
        FocusMovedElsewhere = false;
        if (!IsVisible) Show();
        Activate();
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle()?.Handle is { } hwnd)
        {
            bool plain = Native.WindowsNative.IsForeground(hwnd);
            bool ok = plain || Native.WindowsNative.ForceForeground(hwnd);
            Log?.Invoke($"popup foreground: {(plain ? "ok (Activate)" : ok ? "ok (forced)" : "FAILED")}");
        }
        Focus();
    }

    // ウィンドウを閉じても破棄せず再利用する（常駐アプリの Hide/Show と同じ扱い）
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
