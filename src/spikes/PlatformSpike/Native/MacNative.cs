using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PlatformSpike.Native;

/// <summary>macOS のフォーカス返却の検証用（objc ランタイムを直接呼ぶ）。</summary>
[SupportedOSPlatform("macos")]
internal static class MacNative
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint sel, nint arg);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern int SendInt(nint receiver, nint sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern bool SendBool(nint receiver, nint sel, nuint arg);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern bool SendBool(nint receiver, nint sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint SendPid(nint receiver, nint sel, int pid);

    private static nint Sel(string s) => sel_registerName(s);

    private static nint FrontmostApp()
    {
        nint ws = Send(objc_getClass("NSWorkspace"), Sel("sharedWorkspace"));
        return Send(ws, Sel("frontmostApplication"));
    }

    /// <summary>前面アプリの pid（取れなければ 0）。</summary>
    public static int FrontmostPid()
    {
        nint app = FrontmostApp();
        return app == 0 ? 0 : SendInt(app, Sel("processIdentifier"));
    }

    public static string FrontmostName()
    {
        nint app = FrontmostApp();
        if (app == 0) return "(none)";
        nint name = Send(app, Sel("localizedName"));
        return name == 0 ? "?" : Marshal.PtrToStringUTF8(Send(name, Sel("UTF8String"))) ?? "?";
    }

    /// <summary>
    /// 方式 A: 直前アプリへ明示的に activate を譲る。
    /// macOS 14+ は協調的アクティベーションなので yieldActivationToApplication: を先に呼ぶ。
    /// </summary>
    public static bool ActivatePid(int pid)
    {
        nint app = SendPid(objc_getClass("NSRunningApplication"), Sel("runningApplicationWithProcessIdentifier:"), pid);
        if (app == 0) return false;
        nint nsApp = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
        nint yieldSel = Sel("yieldActivationToApplication:");
        if (SendBool(nsApp, Sel("respondsToSelector:"), (nuint)yieldSel))
            Send(nsApp, yieldSel, app);
        nint activateSel = Sel("activate");
        if (SendBool(app, Sel("respondsToSelector:"), (nuint)activateSel))
            return SendBool(app, activateSel);
        return SendBool(app, Sel("activateWithOptions:"), 0);
    }

    /// <summary>方式 B: 自アプリを隠す（OS が直前のアプリを前面に戻す）。全ウィンドウが隠れる。</summary>
    public static void HideApp()
    {
        nint nsApp = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
        Send(nsApp, Sel("hide:"), 0);
    }
}

/// <summary>§13.1-1 デスクトップ判定の検証: カーソル下のウィンドウ（前面から順）を CGWindowList で調べる。</summary>
[SupportedOSPlatform("macos")]
internal static class MacWindowList
{
    private const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect { public double X, Y, W, H; }

    [DllImport(CG)] private static extern nint CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
    [DllImport(CG)] private static extern bool CGRectMakeWithDictionaryRepresentation(nint dict, out CGRect rect);
    [DllImport(CF)] private static extern long CFArrayGetCount(nint array);
    [DllImport(CF)] private static extern nint CFArrayGetValueAtIndex(nint array, long index);
    [DllImport(CF)] private static extern nint CFDictionaryGetValue(nint dict, nint key);
    [DllImport(CF)] private static extern nint CFStringCreateWithCString(nint alloc, string s, uint encoding);
    [DllImport(CF)] private static extern bool CFStringGetCString(nint s, byte[] buffer, long size, uint encoding);
    [DllImport(CF)] private static extern bool CFNumberGetValue(nint number, int type, out long value);
    [DllImport(CF)] private static extern void CFRelease(nint obj);

    private const uint kCGWindowListOptionOnScreenOnly = 1;
    private const uint kCFStringEncodingUTF8 = 0x08000100;
    private const int kCFNumberSInt64Type = 4;

    private static readonly nint KeyLayer = Str("kCGWindowLayer");
    private static readonly nint KeyOwner = Str("kCGWindowOwnerName");
    private static readonly nint KeyName = Str("kCGWindowName");
    private static readonly nint KeyBounds = Str("kCGWindowBounds");
    private static readonly nint KeyPid = Str("kCGWindowOwnerPID");

    private static nint Str(string s) => CFStringCreateWithCString(0, s, kCFStringEncodingUTF8);

    private static string? GetString(nint dict, nint key)
    {
        nint v = CFDictionaryGetValue(dict, key);
        if (v == 0) return null;
        var buf = new byte[512];
        return CFStringGetCString(v, buf, buf.Length, kCFStringEncodingUTF8)
            ? System.Text.Encoding.UTF8.GetString(buf, 0, Array.IndexOf(buf, (byte)0)) : "?";
    }

    private static long GetLong(nint dict, nint key)
    {
        nint v = CFDictionaryGetValue(dict, key);
        return v != 0 && CFNumberGetValue(v, kCFNumberSInt64Type, out long r) ? r : long.MinValue;
    }

    /// <summary>点を含むウィンドウを前面から最大 max 件。座標は CG のグローバル座標（左上原点・pt）。</summary>
    public static List<string> WindowsAt(double x, double y, int max = 4)
    {
        var result = new List<string>();
        nint list = CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly, 0);
        if (list == 0) return result;
        try
        {
            long n = CFArrayGetCount(list);
            for (long i = 0; i < n && result.Count < max; i++)
            {
                nint d = CFArrayGetValueAtIndex(list, i);
                nint b = CFDictionaryGetValue(d, KeyBounds);
                if (b == 0 || !CGRectMakeWithDictionaryRepresentation(b, out var r)) continue;
                if (x < r.X || y < r.Y || x >= r.X + r.W || y >= r.Y + r.H) continue;
                result.Add($"layer={GetLong(d, KeyLayer)} owner={GetString(d, KeyOwner)} pid={GetLong(d, KeyPid)} name={GetString(d, KeyName) ?? "(null)"} bounds=({r.X},{r.Y},{r.W}x{r.H})");
            }
        }
        finally { CFRelease(list); }
        return result;
    }
}
