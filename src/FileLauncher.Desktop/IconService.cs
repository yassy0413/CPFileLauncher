using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform;

namespace FileLauncher.App;

/// <summary>
/// アイテムのアイコン（SPEC §5.4）。メモリ → icons/*.png → OS の順に引く。OS からの取得は非同期で、
/// 終わるまでは汎用アイコンを出し、取得後に onLoaded で差し替える。png の読み書きは UI スレッドで行う（1 件 1〜5ms）。
/// </summary>
internal sealed class IconService
{
    private readonly IIconProvider _provider;
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, Bitmap> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);

    public IconService(IIconProvider provider, string cacheDirectory)
    {
        _provider = provider;
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(cacheDirectory);
    }

    /// <summary>キャッシュ済みなら即座に返す。無ければ null を返して取得を開始し、終わったら onLoaded（UI スレッド）。</summary>
    public IImage? Get(LauncherItem item, int px, bool allowThumbnail, Action<IImage> onLoaded)
    {
        bool hasOverride = !string.IsNullOrEmpty(item.IconOverride);
        string variant = hasOverride ? "override" : allowThumbnail ? "t1" : "t0";
        string source = IconCacheKey.SourceOf(item);
        string path = Path.Combine(_cacheDirectory, IconCacheKey.FileName(source, px, variant));

        if (_memory.TryGetValue(path, out var cached)) return cached;
        if (File.Exists(path))
        {
            try
            {
                var bmp = new Bitmap(path);
                _memory[path] = bmp;
                return bmp;
            }
            catch (Exception ex)
            {
                AppLog.Info($"icon cache 読込失敗 {path}: {ex.Message}");
            }
        }

        if (_loading.Add(path)) _ = LoadAsync(item, px, allowThumbnail, hasOverride, path, onLoaded);
        return null;
    }

    private async Task LoadAsync(LauncherItem item, int px, bool allowThumbnail, bool hasOverride, string path, Action<IImage> onLoaded)
    {
        try
        {
            Bitmap? bmp = null;
            if (hasOverride) bmp = LoadOverride(LaunchArgs.ExpandPath(item.IconOverride!), px);
            if (bmp is null)
            {
                var pixels = await _provider.GetIconAsync(item, px, allowThumbnail); // 継続は UI スレッド
                if (pixels is null) return;
                bmp = ToBitmap(pixels);
            }
            bmp.Save(path);
            _memory[path] = bmp;
            onLoaded(bmp);
        }
        catch (Exception ex)
        {
            AppLog.Info($"icon 取得失敗 '{item.Name}': {ex.Message}");
        }
        finally
        {
            _loading.Remove(path);
        }
    }

    /// <summary>ユーザー指定のアイコン画像（png / jpg / bmp / ico。svg / icns は未対応 → OS のアイコンにフォールバック）。</summary>
    private static Bitmap? LoadOverride(string file, int px)
    {
        try
        {
            using var original = new Bitmap(file);
            double scale = Math.Min((double)px / original.PixelSize.Width, (double)px / original.PixelSize.Height);
            var size = new PixelSize(Math.Max(1, (int)(original.PixelSize.Width * scale)), Math.Max(1, (int)(original.PixelSize.Height * scale)));
            return original.CreateScaledBitmap(size, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex)
        {
            AppLog.Info($"上書きアイコンを読めない {file}: {ex.Message}");
            return null;
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

    /// <summary>キャッシュを全消去（設定変更でサイズが変わったとき等。M5 の「アイコンを再取得」用）。</summary>
    public void ClearMemory() => _memory.Clear();
}
