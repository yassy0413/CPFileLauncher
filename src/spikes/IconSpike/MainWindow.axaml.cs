using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using IconSpike.Native;

namespace IconSpike;

/// <summary>
/// アイコン取得スパイク。検証対象（SPEC §5.1 アイコン / §9.2 icons/ / §10.3 IIconProvider / §11 表示遅延）:
///   - IShellItemImageFactory と SHGetFileInfo(システムイメージリスト) の画質・速度・alpha の扱い
///   - exe / フォルダ / ドライブ / .lnk（自身とリンク先）/ 画像 / 存在しないパス / URL（既定ブラウザ）/ 特殊フォルダ
///   - 専用 STA スレッドでの非同期取得、png キャッシュの書き込みと読み込み時間
///   - DPI スケーリング（要求ピクセル = 論理サイズ × RenderScaling）
/// </summary>
public partial class MainWindow : Window
{
    private readonly StaWorker _sta = new("IconSTA");
    private readonly IconCache _cache = new(Path.Combine(AppContext.BaseDirectory, "iconcache"));
    private readonly List<string> _inputs = new();
    private bool _busy;

    // 1 回の一括取得の集計
    private int _batchTiles, _batchCacheHits;
    private double _batchMs;

    public MainWindow()
    {
        InitializeComponent();

        SamplesButton.Click += async (_, _) => await AddInputsAsync(Samples());
        ReloadButton.Click += async (_, _) => await ReloadAllAsync("再取得");
        ClearTilesButton.Click += (_, _) => { _inputs.Clear(); TilesPanel.Children.Clear(); };
        ClearCacheButton.Click += (_, _) => { _cache.Clear(); Log("cache cleared"); };
        OpenCacheButton.Click += (_, _) => Process.Start("explorer.exe", _cache.Directory);

        SizeBox.SelectionChanged += async (_, _) => await ReloadAllAsync("サイズ変更");
        IconOnlyCheck.IsCheckedChanged += async (_, _) => await ReloadAllAsync("アイコンのみ 切替");
        CompareSysCheck.IsCheckedChanged += async (_, _) => await ReloadAllAsync("比較 切替");
        DarkBgCheck.IsCheckedChanged += async (_, _) => await ReloadAllAsync("背景切替");

        ClearLogButton.Click += (_, _) => LogBox.Text = string.Empty;
        CopyLogButton.Click += async (_, _) =>
        {
            if (Clipboard is { } cb) await cb.SetTextAsync(LogBox.Text ?? string.Empty);
        };

        DragDrop.SetAllowDrop(DropZone, true);
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);

        Opened += async (_, _) =>
        {
            Log($"OS={RuntimeInformation.OSDescription} .NET={Environment.Version} scale={RenderScaling}");
            Log($"cache dir: {_cache.Directory}");
            var (n, bytes) = _cache.Stats();
            Log($"cache: {n} files, {bytes / 1024.0:F1} KB");

            if (SelfTest)
            {
                _cache.Clear();
                await AddInputsAsync(Samples());
                await ReloadAllAsync("selftest: キャッシュから再取得");
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.log"), LogBox.Text);
                Close();
            }
        };
    }

    public bool SelfTest { get; init; }

    private int SelectedSize =>
        SizeBox.SelectedItem is ComboBoxItem { Content: string s } && int.TryParse(s, out var v) ? v : 48;

    // ---------------- 入力 ----------------

#pragma warning disable CS0618 // スパイクなので旧 D&D API のまま（製品は DataTransfer を使う）
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool ok = e.Data.Contains(DataFormats.Files) || e.Data.Contains(DataFormats.Text);
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var list = new List<string>();
        if (e.Data.Contains(DataFormats.Files) && e.Data.GetFiles() is { } files)
        {
            foreach (var f in files)
                if (f.TryGetLocalPath() is { } p) list.Add(p);
        }
        else if (e.Data.Contains(DataFormats.Text) && e.Data.GetText() is { } text)
        {
            list.Add(text.Trim());
        }
        e.Handled = true;
        await AddInputsAsync(list);
    }
#pragma warning restore CS0618

    private static IEnumerable<string> Samples()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<string>
        {
            Path.Combine(win, "System32", "notepad.exe"),
            Path.Combine(win, "explorer.exe"),
            Path.Combine(win, "System32", "cmd.exe"),
            win,                                    // フォルダ
            @"C:\",                                 // ドライブ
            Path.Combine(home, "Desktop"),          // 特殊フォルダ（実パス）
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(win, "win.ini"),           // 関連付けのある文書
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", // PC（シェル名）
            "::{645FF040-5081-101B-9F08-00AA002F954E}", // ごみ箱（シェル名）
            @"C:\nope\missing.xlsx",                // 存在しないファイル
            @"C:\nope\missing_folder\",             // 存在しないフォルダ
            "https://example.com",                  // URL → 既定ブラウザ
        };

        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var dir in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                     Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                 })
        {
            try { list.AddRange(Directory.EnumerateFiles(dir, "*.lnk", opts).Take(4)); } catch { }
        }
        try
        {
            var pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var img = Directory.EnumerateFiles(pics, "*.*", opts)
                .FirstOrDefault(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (img is not null) list.Add(img); // サムネイル検証用
        }
        catch { }
        return list;
    }

    private async Task AddInputsAsync(IEnumerable<string> inputs)
    {
        var added = inputs.Where(i => !string.IsNullOrWhiteSpace(i) && !_inputs.Contains(i)).ToList();
        _inputs.AddRange(added);
        await RunBatchAsync(added, $"追加 {added.Count} 件");
    }

    private async Task ReloadAllAsync(string reason)
    {
        if (_inputs.Count == 0) return;
        TilesPanel.Children.Clear();
        await RunBatchAsync(_inputs.ToList(), reason);
    }

    private async Task RunBatchAsync(List<string> inputs, string reason)
    {
        if (_busy || inputs.Count == 0) return;
        _busy = true;
        _batchTiles = _batchCacheHits = 0;
        _batchMs = 0;
        int px = PixelSizeFor(SelectedSize);
        Log($"--- {reason}: size={SelectedSize} → {px}px (scale={RenderScaling}) iconOnly={IconOnlyCheck.IsChecked} cache={UseCacheCheck.IsChecked}");
        var total = Stopwatch.StartNew();
        try
        {
            foreach (var input in inputs) await AddInputTilesAsync(input, px);
        }
        finally { _busy = false; }
        Log($"--- 完了: タイル {_batchTiles} 枚 (cache hit {_batchCacheHits}) / 取得+描画準備の合計 {_batchMs:F1} ms / 経過 {total.ElapsedMilliseconds} ms");
        var (n, bytes) = _cache.Stats();
        StatusText.Text = $"cache: {n} files, {bytes / 1024.0:F1} KB";
    }

    private int PixelSizeFor(int logical) => (int)Math.Round(logical * RenderScaling);

    // ---------------- 1 入力 → タイル群 ----------------

    private async Task AddInputTilesAsync(string input, int px)
    {
        var flags = IconOnlyCheck.IsChecked == true ? SIIGBF.IconOnly : SIIGBF.ResizeToFit;

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            var exe = await _sta.Run(() => ShellIcons.DefaultHandlerExe(uri.Scheme));
            Log($"URL {input} → handler {exe ?? "(なし)"}");
            if (exe is not null) await AddTileAsync($"URL {uri.Host}", exe, px, Method.Factory, flags, true, false);
            return;
        }

        if (input.StartsWith("::") || input.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            await AddTileAsync(input, input, px, Method.Factory, flags, true, false);
            return;
        }

        bool isDir = Directory.Exists(input);
        bool exists = isDir || File.Exists(input);
        string name = Path.GetFileName(input.TrimEnd('\\')) is { Length: > 0 } n ? n : input;

        if (!exists)
        {
            // 存在しないパス: シェルアイテムが作れないので拡張子から引く（SPEC §5.1 グレーアウト表示の元画像）
            await AddTileAsync($"{name} (存在しない)", input, px, Method.SystemImageList, flags, false, input.EndsWith('\\'));
            return;
        }

        await AddTileAsync(name, input, px, Method.Factory, flags, true, isDir);
        if (CompareSysCheck.IsChecked == true)
            await AddTileAsync(name, input, px, Method.SystemImageList, flags, true, isDir);

        if (input.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            ShellIcons.LinkInfo li;
            try { li = await _sta.Run(() => ShellIcons.ReadLink(input)); }
            catch (Exception ex) { Log($"LNK {name}: read failed {ex.Message}"); return; }

            Log($"LNK {name}: target='{li.Target}' parsing='{li.ParsingName}' args='{li.Arguments}' wd='{li.WorkingDirectory}' icon='{li.IconLocation}'");
            if (string.IsNullOrEmpty(li.Target))
            {
                if (string.IsNullOrEmpty(li.ParsingName))
                {
                    Log($"LNK {name}: target も ID リストも無し → .lnk 自身のアイコンを使うしかない");
                    return;
                }
                // ファイルではなくシェル項目を指すリンク（エクスプローラー、PC、ストアアプリ等）
                Log($"LNK {name}: シェル項目へのリンク → '{li.ParsingName}' のアイコンを取得");
                await AddTileAsync($"→ {li.ParsingName}", li.ParsingName, px, Method.Factory, flags, true, false);
                return;
            }
            bool tDir = Directory.Exists(li.Target);
            bool tExists = tDir || File.Exists(li.Target);
            await AddTileAsync($"→ {Path.GetFileName(li.Target)}", li.Target, px,
                tExists ? Method.Factory : Method.SystemImageList, flags, tExists, tDir);
        }
    }

    private enum Method { Factory, SystemImageList }

    private async Task AddTileAsync(string title, string source, int px, Method method, SIIGBF flags, bool exists, bool isDir)
    {
        string variant = method == Method.Factory ? $"factory-{(int)flags}" : $"sys-{exists}";
        string methodLabel = method == Method.Factory ? "Factory" : "SHGetFileInfo";
        string cachePath = _cache.PathFor(source, px, variant);
        bool useCache = UseCacheCheck.IsChecked == true;

        try
        {
            if (useCache && File.Exists(cachePath))
            {
                var sw = Stopwatch.StartNew();
                var bmp = new Bitmap(cachePath);
                double ms = sw.Elapsed.TotalMilliseconds;
                _batchMs += ms;
                _batchCacheHits++;
                AddTile(bmp, title, $"{methodLabel} {bmp.PixelSize.Width}x{bmp.PixelSize.Height}\ncache 読込 {ms:F1} ms");
                Log($"{methodLabel,-13} {title}: cache {bmp.PixelSize.Width}x{bmp.PixelSize.Height} load {ms:F1} ms");
                return;
            }

            var (pixels, extractMs) = await _sta.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var p = method == Method.Factory
                    ? ShellIcons.FromImageFactory(source, px, flags)
                    : ShellIcons.FromSystemImageList(source, px, exists, isDir);
                return (p, sw.Elapsed.TotalMilliseconds);
            });

            var sw2 = Stopwatch.StartNew();
            var wb = ToBitmap(pixels);
            double convertMs = sw2.Elapsed.TotalMilliseconds;
            double saveMs = 0;
            if (useCache)
            {
                sw2.Restart();
                wb.Save(cachePath);
                saveMs = sw2.Elapsed.TotalMilliseconds;
            }
            _batchMs += extractMs + convertMs;

            AddTile(wb, title,
                $"{methodLabel} {pixels.Width}x{pixels.Height} {(pixels.Premultiplied ? "premul" : "straight")}\n" +
                $"抽出 {extractMs:F1} / 変換 {convertMs:F1} / 保存 {saveMs:F1} ms\n{pixels.Note}");
            Log($"{methodLabel,-13} {title}: {pixels.Width}x{pixels.Height} extract {extractMs:F1} ms, convert {convertMs:F1} ms, png save {saveMs:F1} ms {pixels.Note}");
        }
        catch (Exception ex)
        {
            AddTile(null, title, $"{methodLabel} 失敗\n{ex.GetType().Name}: {ex.Message}");
            Log($"{methodLabel,-13} {title}: FAILED {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
        }
    }

    private static WriteableBitmap ToBitmap(IconPixels p)
    {
        var wb = new WriteableBitmap(new PixelSize(p.Width, p.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, p.Premultiplied ? AlphaFormat.Premul : AlphaFormat.Unpremul);
        using var fb = wb.Lock();
        int stride = p.Width * 4;
        for (int y = 0; y < p.Height; y++)
            Marshal.Copy(p.Bgra, y * stride, fb.Address + y * fb.RowBytes, stride);
        return wb;
    }

    private void AddTile(IImage? image, string title, string detail)
    {
        _batchTiles++;
        int size = SelectedSize;
        bool dark = DarkBgCheck.IsChecked == true;
        var fg = dark ? Brushes.White : Brushes.Black;

        var img = new Image
        {
            Source = image,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);

        TilesPanel.Children.Add(new Border
        {
            Width = Math.Max(size + 24, 170),
            Margin = new Thickness(4),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4),
            Background = dark ? new SolidColorBrush(Color.FromRgb(0x2b, 0x2b, 0x2b)) : Brushes.White,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    img,
                    new TextBlock { Text = title, Foreground = fg, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = detail, Foreground = Brushes.Gray, FontSize = 10, TextWrapping = TextWrapping.Wrap },
                },
            },
        });
    }

    private void Log(string message)
    {
        LogBox.Text += $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
        LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _sta.Dispose();
        base.OnClosed(e);
    }
}
