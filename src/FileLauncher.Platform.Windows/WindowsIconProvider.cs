using System.Runtime.Versioning;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform.Windows.Native;

namespace FileLauncher.Platform.Windows;

/// <summary>
/// アイコン取得（SPEC §5.4 の規則。IconSpike で検証済み）。
/// 実在 → IShellItemImageFactory（画像ファイルはサムネイル許可）、存在しない・失敗 → SHGetFileInfo（拡張子から）。
/// .lnk は .lnk 自身、URL は既定ブラウザ、コマンドは cmd.exe のアイコン。
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsIconProvider : IIconProvider
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff",
    };

    private readonly StaWorker _sta;

    public WindowsIconProvider(StaWorker sta) => _sta = sta;

    public async Task<IconPixels?> GetIconAsync(LauncherItem item, int sizePx, bool allowThumbnail, CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _sta.Run(() => Fetch(item, sizePx, allowThumbnail), cancellationToken);
            return raw is null ? null : new IconPixels(raw.Width, raw.Height, raw.Bgra, raw.Premultiplied);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static RawIcon? Fetch(LauncherItem item, int px, bool allowThumbnail)
    {
        string source = item.Kind switch
        {
            ItemKind.Url => DefaultBrowser(item.Target) ?? "",
            ItemKind.Command => Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe",
            _ => LaunchArgs.ExpandPath(item.LinkPath ?? item.Target),
        };
        if (source.Length == 0) return null;

        bool isDir = Directory.Exists(source);
        bool exists = isDir || File.Exists(source);
        if (exists)
        {
            var flags = allowThumbnail && !isDir && ImageExtensions.Contains(Path.GetExtension(source))
                ? SIIGBF.ResizeToFit
                : SIIGBF.IconOnly;
            try { return ShellIcons.FromImageFactory(source, px, flags); }
            catch (Exception) { /* SHGetFileInfo にフォールバック */ }
        }
        try
        {
            return ShellIcons.FromSystemImageList(source, px, exists, isDir || item.Kind == ItemKind.Folder);
        }
        catch (Exception)
        {
            return null; // 呼び出し側で汎用アイコン
        }
    }

    private static string? DefaultBrowser(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? ShellIcons.DefaultHandlerExe(uri.Scheme) : null;
}
