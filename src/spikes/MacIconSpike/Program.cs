// macOS アイコン取得の検証（使い捨て）。NSWorkspace / CGImageSource を P/Invoke で呼び、BGRA に描いて PNG 保存する。
using System.Diagnostics;
using System.Runtime.InteropServices;

const string ObjC = "/usr/lib/libobjc.A.dylib";
const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
const string IIO = "/System/Library/Frameworks/ImageIO.framework/ImageIO";
NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");

int px = args.Length > 0 ? int.Parse(args[0]) : 96;
string outDir = Path.Combine(AppContext.BaseDirectory, "out");
Directory.CreateDirectory(outDir);

var targets = new (string label, Func<nint> image)[]
{
    ("folder_home", () => IconForFile(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))),
    ("folder_apps", () => IconForFile("/Applications")),
    ("app_safari", () => IconForFile("/Applications/Safari.app")),
    ("app_textedit", () => IconForFile("/System/Applications/TextEdit.app")),
    ("file_md", () => IconForFile("/Users/yasutakaishii/Desktop/work/projects/FileLauncher/CLAUDE.md")),
    ("file_csproj", () => IconForFile("/Users/yasutakaishii/Desktop/work/projects/FileLauncher/src/FileLauncher.sln")),
    ("volume_root", () => IconForFile("/")),
    ("missing_txt", () => IconForFileType("txt")),
    ("missing_pdf", () => IconForFileType("pdf")),
    ("missing_folder", () => IconForFileType("public.folder")),
    ("url_browser", () => DefaultBrowserIcon("https://example.com")),
};

foreach (var (label, getImage) in targets)
{
    var sw = Stopwatch.StartNew();
    nint img = getImage();
    var bgra = img == 0 ? null : Render(img, px);
    sw.Stop();
    Console.WriteLine($"{label,-16} {(bgra is null ? "FAILED" : "ok")} {sw.Elapsed.TotalMilliseconds,7:F1} ms");
}

// 画像サムネイル（CGImageSource）
foreach (var path in Directory.EnumerateFiles("/System/Library/Desktop Pictures", "*.heic").Take(1)
    .Concat(Directory.EnumerateFiles("/Library/User Pictures", "*.*", SearchOption.AllDirectories).Take(1)))
{
    var sw = Stopwatch.StartNew();
    bool ok = Thumbnail(path, px);
    Console.WriteLine($"thumb {Path.GetFileName(path),-30} {(ok ? "ok" : "FAILED")} {sw.Elapsed.TotalMilliseconds,7:F1} ms");
}

// --- NSWorkspace ---
nint IconForFile(string path) => Send1(Workspace(), Sel("iconForFile:"), NSString(path));
nint IconForFileType(string type) => Send1(Workspace(), Sel("iconForFileType:"), NSString(type));
nint DefaultBrowserIcon(string url)
{
    nint nsurl = Send1(objc_getClass("NSURL"), Sel("URLWithString:"), NSString(url));
    nint app = Send1(Workspace(), Sel("URLForApplicationToOpenURL:"), nsurl);
    if (app == 0) return 0;
    nint path = Send(app, Sel("path"));
    Console.WriteLine($"  default browser: {Marshal.PtrToStringUTF8(Send(path, Sel("UTF8String")))}");
    return Send1(Workspace(), Sel("iconForFile:"), path);
}
nint Workspace() => Send(objc_getClass("NSWorkspace"), Sel("sharedWorkspace"));
nint NSString(string s) => Send1(objc_getClass("NSString"), Sel("stringWithUTF8String:"), Marshal.StringToCoTaskMemUTF8(s));

// NSImage → 指定 px の CGImage（最適な表現を選ばせる）→ BGRA 描画
byte[]? Render(nint nsImage, int size)
{
    var rect = new CGRect { W = size, H = size };
    nint cgImage = SendRect(nsImage, Sel("CGImageForProposedRect:context:hints:"), ref rect, 0, 0);
    if (cgImage == 0) return null;
    Console.Write($"  src {CGImageGetWidth(cgImage)}x{CGImageGetHeight(cgImage)} → ");
    return Draw(cgImage, size, "");
}

bool Thumbnail(string path, int size)
{
    nint url = CFURLCreateWithFileSystemPath(0, CFStr(path), 0, false);
    nint src = CGImageSourceCreateWithURL(url, 0);
    if (src == 0) return false;
    nint keys0 = CFStr("kCGImageSourceCreateThumbnailFromImageAlways");
    nint keys1 = CFStr("kCGImageSourceThumbnailMaxPixelSize");
    nint keys2 = CFStr("kCGImageSourceCreateThumbnailWithTransform");
    long sz = size;
    nint num = CFNumberCreate(0, 4, ref sz);
    nint t = Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CF), "kCFBooleanTrue"));
    var keys = new[] { keys0, keys1, keys2 };
    var vals = new[] { t, num, t };
    nint dict = CFDictionaryCreate(0, keys, vals, 3, NativeLibrary.GetExport(NativeLibrary.Load(CF), "kCFTypeDictionaryKeyCallBacks"), NativeLibrary.GetExport(NativeLibrary.Load(CF), "kCFTypeDictionaryValueCallBacks"));
    nint img = CGImageSourceCreateThumbnailAtIndex(src, 0, dict);
    if (img == 0) return false;
    Console.Write($"  thumb {CGImageGetWidth(img)}x{CGImageGetHeight(img)} → ");
    return Draw(img, size, "thumb_" + Path.GetFileNameWithoutExtension(path)) is not null;
}

byte[]? Draw(nint cgImage, int size, string name)
{
    // kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little = BGRA premultiplied
    const uint info = 2 | (2 << 12);
    var buf = new byte[size * size * 4];
    var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
    try
    {
        nint cs = CGColorSpaceCreateDeviceRGB();
        nint ctx = CGBitmapContextCreate(h.AddrOfPinnedObject(), size, size, 8, size * 4, cs, info);
        // アスペクト比を保って中央に描く（サムネイル用）
        double w = CGImageGetWidth(cgImage), hh = CGImageGetHeight(cgImage);
        double scale = Math.Min(size / w, size / hh);
        var r = new CGRect { X = (size - w * scale) / 2, Y = (size - hh * scale) / 2, W = w * scale, H = hh * scale };
        CGContextSetInterpolationQuality(ctx, 3);
        CGContextDrawImage(ctx, r, cgImage);
        nint outImg = CGBitmapContextCreateImage(ctx);
        string file = Path.Combine(outDir, (name == "" ? $"icon_{Counter()}" : name) + ".png");
        SavePng(outImg, file);
        Console.WriteLine(file);
        CGContextRelease(ctx);
        return buf;
    }
    finally { h.Free(); }
}

void SavePng(nint img, string path)
{
    nint url = CFURLCreateWithFileSystemPath(0, CFStr(path), 0, false);
    nint dest = CGImageDestinationCreateWithURL(url, CFStr("public.png"), 1, 0);
    CGImageDestinationAddImage(dest, img, 0);
    CGImageDestinationFinalize(dest);
}

int Counter() => ++_counter.Value;
nint CFStr(string s) => CFStringCreateWithCString(0, s, 0x08000100);
nint Sel(string s) => sel_registerName(s);

[DllImport(ObjC)] static extern nint objc_getClass(string name);
[DllImport(ObjC)] static extern nint sel_registerName(string name);
[DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern nint Send(nint r, nint s);
[DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern nint Send1(nint r, nint s, nint a);
[DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern nint SendRect(nint r, nint s, ref CGRect rect, nint ctx, nint hints);
[DllImport(CG)] static extern nint CGColorSpaceCreateDeviceRGB();
[DllImport(CG)] static extern nint CGBitmapContextCreate(nint data, nint w, nint h, nint bpc, nint bpr, nint cs, uint info);
[DllImport(CG)] static extern void CGContextDrawImage(nint ctx, CGRect r, nint img);
[DllImport(CG)] static extern void CGContextSetInterpolationQuality(nint ctx, int q);
[DllImport(CG)] static extern nint CGBitmapContextCreateImage(nint ctx);
[DllImport(CG)] static extern void CGContextRelease(nint ctx);
[DllImport(CG)] static extern nint CGImageGetWidth(nint img);
[DllImport(CG)] static extern nint CGImageGetHeight(nint img);
[DllImport(CF)] static extern nint CFStringCreateWithCString(nint a, string s, uint enc);
[DllImport(CF)] static extern nint CFURLCreateWithFileSystemPath(nint a, nint path, int style, bool isDir);
[DllImport(CF)] static extern nint CFNumberCreate(nint a, int type, ref long v);
[DllImport(CF)] static extern nint CFDictionaryCreate(nint a, nint[] keys, nint[] vals, nint n, nint kcb, nint vcb);
[DllImport(IIO)] static extern nint CGImageSourceCreateWithURL(nint url, nint opts);
[DllImport(IIO)] static extern nint CGImageSourceCreateThumbnailAtIndex(nint src, nint i, nint opts);
[DllImport(IIO)] static extern nint CGImageDestinationCreateWithURL(nint url, nint type, nint n, nint opts);
[DllImport(IIO)] static extern void CGImageDestinationAddImage(nint d, nint img, nint props);
[DllImport(IIO)] static extern bool CGImageDestinationFinalize(nint d);

[StructLayout(LayoutKind.Sequential)] struct CGRect { public double X, Y, W, H; }
partial class Program { }
static class _counter { public static int Value; }
