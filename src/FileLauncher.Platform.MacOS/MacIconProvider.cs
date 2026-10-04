using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Platform.MacOS.Native.ObjC;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// アイコン取得の macOS 版（SPEC §5.4。src/spikes/MacIconSpike で検証済み）。
/// 実在 → NSWorkspace iconForFile:（画像ファイルはサムネイル許可なら CGImageSource のサムネイル）、
/// 存在しない → iconForContentType:（拡張子から UTType）。URL は既定ブラウザ、コマンドはターミナルのアイコン。
/// シンボリックリンクは実体に解決してから取る（/Applications/Safari.app 等に矢印が付かないように）。
/// Finder エイリアスは .lnk と同じくエイリアス自身（LinkPath）のアイコン。
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacIconProvider : IIconProvider
{
    private const string TerminalApp = "/System/Applications/Utilities/Terminal.app";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff",
    };

    private readonly MacWorker _worker;

    public MacIconProvider(MacWorker worker) => _worker = worker;

    public async Task<IconPixels?> GetIconAsync(LauncherItem item, int sizePx, bool allowThumbnail, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _worker.Run(() => Fetch(item, sizePx, allowThumbnail), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static IconPixels? Fetch(LauncherItem item, int px, bool allowThumbnail)
    {
        string source = item.Kind switch
        {
            ItemKind.Url => DefaultHandlerApp(item.Target) ?? "",
            ItemKind.Command => TerminalApp,
            _ => ResolveSymlink(LaunchArgs.ExpandPath(item.LinkPath ?? item.Target)),
        };
        if (source.Length == 0) return null;

        bool isDir = Directory.Exists(source);
        bool exists = isDir || File.Exists(source);
        try
        {
            if (exists && allowThumbnail && !isDir && ImageExtensions.Contains(Path.GetExtension(source))
                && Thumbnail(source, px) is { } thumb)
                return thumb;

            nint image = exists
                ? Send(SharedWorkspace, Sel("iconForFile:"), NSString(source))
                : ContentType(source, item) is var type and not 0
                    ? Send(SharedWorkspace, Sel("iconForContentType:"), type)
                    : 0;
            return image == 0 ? null : Render(image, px);
        }
        catch (Exception)
        {
            return null; // 呼び出し側で汎用アイコン
        }
    }

    /// <summary>存在しないパスの種類（UTType）。拡張子から、無ければフォルダ / アプリ / 汎用データ。</summary>
    private static nint ContentType(string path, LauncherItem item)
    {
        nint utType = Class("UTType");
        if (item.Kind == ItemKind.App && path.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return Send(utType, Sel("typeWithIdentifier:"), NSString("com.apple.application-bundle"));
        if (item.Kind == ItemKind.Folder) return Send(utType, Sel("typeWithIdentifier:"), NSString("public.folder"));
        string ext = Path.GetExtension(path.TrimEnd('/'));
        nint byExt = ext.Length > 1 ? Send(utType, Sel("typeWithFilenameExtension:"), NSString(ext[1..])) : 0;
        return byExt != 0 ? byExt : Send(utType, Sel("typeWithIdentifier:"), NSString("public.data"));
    }

    private static string ResolveSymlink(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path.TrimEnd('/')) : new FileInfo(path);
            return info.LinkTarget is null ? path : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>URL を開く既定アプリ（ブラウザ・メーラー）の .app パス。</summary>
    internal static string? DefaultHandlerApp(string url)
    {
        nint nsurl = Send(Class("NSURL"), Sel("URLWithString:"), NSString(url));
        if (nsurl == 0) return null;
        nint app = Send(SharedWorkspace, Sel("URLForApplicationToOpenURL:"), nsurl);
        return app == 0 ? null : ToManagedString(Send(app, Sel("path")));
    }

    /// <summary>NSImage → 要求サイズに最適な表現の CGImage → BGRA 乗算済みに描画。</summary>
    private static IconPixels? Render(nint nsImage, int px)
    {
        var rect = new CGRect { Width = px, Height = px };
        nint cgImage = SendRect(nsImage, Sel("CGImageForProposedRect:context:hints:"), ref rect, 0, 0);
        return cgImage == 0 ? null : Draw(cgImage, px); // cgImage は NSImage 所有（autorelease）なので解放しない
    }

    private static IconPixels? Thumbnail(string path, int px)
    {
        nint cfPath = CoreFoundation.CreateString(path);
        nint url = CoreFoundation.CFURLCreateWithFileSystemPath(0, cfPath, 0, false);
        CoreFoundation.CFRelease(cfPath);
        if (url == 0) return null;
        nint src = ImageIO.CGImageSourceCreateWithURL(url, 0);
        CoreFoundation.CFRelease(url);
        if (src == 0) return null;

        nint kAlways = CoreFoundation.CreateString("kCGImageSourceCreateThumbnailFromImageAlways");
        nint kMax = CoreFoundation.CreateString("kCGImageSourceThumbnailMaxPixelSize");
        nint kTransform = CoreFoundation.CreateString("kCGImageSourceCreateThumbnailWithTransform");
        long size = px;
        nint num = CoreFoundation.CFNumberCreate(0, CoreFoundation.kCFNumberSInt64Type, ref size);
        nint t = CoreFoundation.BooleanTrue;
        nint options = CoreFoundation.CFDictionaryCreate(0, [kAlways, kMax, kTransform], [t, num, t], 3,
            CoreFoundation.TypeDictionaryKeyCallBacks, CoreFoundation.TypeDictionaryValueCallBacks);
        try
        {
            nint image = ImageIO.CGImageSourceCreateThumbnailAtIndex(src, 0, options);
            if (image == 0) return null;
            try { return Draw(image, px); }
            finally { CoreGraphics.CGImageRelease(image); }
        }
        finally
        {
            foreach (var o in new[] { options, num, kAlways, kMax, kTransform, src }) CoreFoundation.CFRelease(o);
        }
    }

    /// <summary>アスペクト比を保って px×px の中央に描く。</summary>
    private static IconPixels? Draw(nint cgImage, int px)
    {
        double w = CoreGraphics.CGImageGetWidth(cgImage), h = CoreGraphics.CGImageGetHeight(cgImage);
        if (w <= 0 || h <= 0) return null;

        var bgra = new byte[px * px * 4];
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        nint cs = CoreGraphics.CGColorSpaceCreateDeviceRGB();
        try
        {
            nint ctx = CoreGraphics.CGBitmapContextCreate(handle.AddrOfPinnedObject(), px, px, 8, px * 4, cs,
                CoreGraphics.BitmapInfoBgraPremultiplied);
            if (ctx == 0) return null;
            try
            {
                double scale = Math.Min(px / w, px / h);
                var r = new CGRect { X = (px - w * scale) / 2, Y = (px - h * scale) / 2, Width = w * scale, Height = h * scale };
                CoreGraphics.CGContextSetInterpolationQuality(ctx, 3); // kCGInterpolationHigh
                CoreGraphics.CGContextDrawImage(ctx, r, cgImage);
            }
            finally { CoreGraphics.CGContextRelease(ctx); }
            // CGBitmapContext のメモリは上の行から（CG は左下原点で描くが、バッファは上から下の並び）
            return new IconPixels(px, px, bgra, Premultiplied: true);
        }
        finally
        {
            CoreGraphics.CGColorSpaceRelease(cs);
            handle.Free();
        }
    }
}
