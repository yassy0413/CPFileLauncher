using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileLauncher.Platform.MacOS.Native;

[SupportedOSPlatform("macos")]
internal static class CoreGraphics
{
    private const string Lib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    /// <summary>kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little = メモリ上 BGRA（乗算済み）。</summary>
    public const uint BitmapInfoBgraPremultiplied = 2 | (2 << 12);

    /// <summary>kCGDesktopIconWindowLevelKey。最背面（常駐の Bottommost）はこのレベル + 1。</summary>
    public const int kCGDesktopIconWindowLevelKey = 18;

    [DllImport(Lib)] public static extern int CGWindowLevelForKey(int key);
    /// <summary>kCGEventSourceStateCombinedSessionState = 0。戻り値は CGEventFlags。</summary>
    [DllImport(Lib)] public static extern ulong CGEventSourceFlagsState(int stateId);

    public const ulong kCGEventFlagMaskShift = 0x20000;
    public const ulong kCGEventFlagMaskControl = 0x40000;
    public const ulong kCGEventFlagMaskAlternate = 0x80000;
    public const ulong kCGEventFlagMaskCommand = 0x100000;

    [DllImport(Lib)] public static extern nint CGEventCreate(nint source);
    [DllImport(Lib)] public static extern CGPoint CGEventGetLocation(nint ev);
    [DllImport(Lib)] public static extern nint CGColorSpaceCreateDeviceRGB();
    [DllImport(Lib)] public static extern void CGColorSpaceRelease(nint cs);
    [DllImport(Lib)] public static extern nint CGBitmapContextCreate(nint data, nint width, nint height, nint bitsPerComponent, nint bytesPerRow, nint colorSpace, uint bitmapInfo);
    [DllImport(Lib)] public static extern void CGContextRelease(nint ctx);
    [DllImport(Lib)] public static extern void CGContextDrawImage(nint ctx, CGRect rect, nint image);
    [DllImport(Lib)] public static extern void CGContextSetInterpolationQuality(nint ctx, int quality);
    [DllImport(Lib)] public static extern void CGImageRelease(nint image);
    [DllImport(Lib)] public static extern nint CGImageGetWidth(nint image);
    [DllImport(Lib)] public static extern nint CGImageGetHeight(nint image);
}

[SupportedOSPlatform("macos")]
internal static class ImageIO
{
    private const string Lib = "/System/Library/Frameworks/ImageIO.framework/ImageIO";

    [DllImport(Lib)] public static extern nint CGImageSourceCreateWithURL(nint url, nint options);
    [DllImport(Lib)] public static extern nint CGImageSourceCreateThumbnailAtIndex(nint source, nint index, nint options);
}
