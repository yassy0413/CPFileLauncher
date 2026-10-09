using System.Runtime.Versioning;
using FileLauncher.Core.Input;
using FileLauncher.Core.Model;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Platform.MacOS.Native.ObjC;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// 盤面ウィンドウの macOS 依存処理（SPEC §3.3、PlatformSpike D/F で確認）。
/// 「前面」はウィンドウではなくアプリ単位なので、CaptureForeground / RestoreForeground は前面アプリの pid を扱う。
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacWindowService : IWindowService
{
    private const long NSApplicationActivationPolicyAccessory = 1;
    private const long NSNormalWindowLevel = 0;
    private const long NSFloatingWindowLevel = 3;

    // NSWindowCollectionBehavior
    private const long CanJoinAllSpaces = 1 << 0;
    private const long MoveToActiveSpace = 1 << 1;
    private const long Stationary = 1 << 4;
    private const long FullScreenAuxiliary = 1 << 8;

    /// <summary>フック・Avalonia・CG の画面座標はどれも左上原点の pt（PlatformSpike E）。</summary>
    public bool UsesLogicalScreenCoordinates => true;

    /// <summary>
    /// Dock・⌘Tab に出さない（.app では LSUIElement、dotnet run でも効くよう実行時に accessory ポリシーにする）。
    /// Space との関係: ポップアップは今いる Space に出す、常駐は全 Space に固定（SPEC §3.5）。
    /// 最前面はウィンドウレベルで制御する（Avalonia の Topmost と二重にしない）。
    /// </summary>
    public void ConfigureBoardWindow(nint windowHandle, DisplayMode mode)
    {
        UseAccessoryActivationPolicy();
        nint window = NSWindowOf(windowHandle);
        if (window == 0) return;
        using var _ = AutoreleasePool.Push();
        long behavior = mode == DisplayMode.Resident ? CanJoinAllSpaces | Stationary : MoveToActiveSpace | FullScreenAuxiliary;
        SendVoidLong(window, Sel("setCollectionBehavior:"), behavior);
        if (mode == DisplayMode.Popup) SendVoidLong(window, Sel("setLevel:"), NSFloatingWindowLevel);
    }

    public static void UseAccessoryActivationPolicy()
    {
        using var _ = AutoreleasePool.Push();
        SendRetBoolLong(SharedApplication, Sel("setActivationPolicy:"), NSApplicationActivationPolicyAccessory);
    }

    public void SetZOrder(nint windowHandle, ZOrder order)
    {
        nint window = NSWindowOf(windowHandle);
        if (window == 0) return;
        using var _ = AutoreleasePool.Push();
        long level = order switch
        {
            ZOrder.Topmost => NSFloatingWindowLevel,
            ZOrder.Bottommost => CoreGraphics.CGWindowLevelForKey(CoreGraphics.kCGDesktopIconWindowLevelKey) + 1,
            _ => NSNormalWindowLevel,
        };
        SendVoidLong(window, Sel("setLevel:"), level);
    }

    /// <summary>前面アプリの pid（取れなければ 0）。</summary>
    public nint CaptureForeground()
    {
        using var _ = AutoreleasePool.Push();
        nint app = Send(SharedWorkspace, Sel("frontmostApplication"));
        if (app == 0) return 0;
        int pid = SendRetInt(app, Sel("processIdentifier"));
        return pid == Environment.ProcessId ? 0 : pid;
    }

    public bool BringToForeground(nint windowHandle)
    {
        nint window = NSWindowOf(windowHandle);
        if (window == 0) return false;
        using var _ = AutoreleasePool.Push();
        nint nsApp = SharedApplication;
        // macOS 14+ は activate、それ以前は activateIgnoringOtherApps:
        if (RespondsTo(nsApp, "activate")) Send(nsApp, Sel("activate"));
        else SendVoidLong(nsApp, Sel("activateIgnoringOtherApps:"), 1);
        SendVoid(window, Sel("makeKeyAndOrderFront:"), 0);
        // macOS のアクティブ化は非同期で、この時点の isActive / isKeyWindow はまだ false のことが多い。
        // 要求を出せたら成功とする（実際に前面化することは製品の確認で見ている）
        return true;
    }

    /// <summary>
    /// 直前のアプリへ返す。macOS 14+ は協調的アクティベーションなので、自アプリから
    /// yieldActivationToApplication: で譲ってから相手を activate する（PlatformSpike F 方式 A）。
    /// </summary>
    public bool PrefersReducedMotion
    {
        get
        {
            using var _ = AutoreleasePool.Push();
            return SendRetBool(SharedWorkspace, Sel("accessibilityDisplayShouldReduceMotion"));
        }
    }

    public void HideApplication()
    {
        using var _ = AutoreleasePool.Push();
        SendVoid(SharedApplication, Sel("hide:"), 0);
    }

    public bool RestoreForeground(nint previous)
    {
        if (previous == 0) return false;
        using var _ = AutoreleasePool.Push();
        nint app = SendInt(Class("NSRunningApplication"), Sel("runningApplicationWithProcessIdentifier:"), (int)previous);
        if (app == 0) return false;
        nint nsApp = SharedApplication;
        if (RespondsTo(nsApp, "yieldActivationToApplication:")) SendVoid(nsApp, Sel("yieldActivationToApplication:"), app);
        if (RespondsTo(app, "activate")) return SendRetBool(app, Sel("activate"));
        return SendRetBoolLong(app, Sel("activateWithOptions:"), 0);
    }

    /// <summary>グローバル座標（左上原点・pt）。scale=1 ではフック・Avalonia の座標と一致（PlatformSpike E）。</summary>
    public ScreenPoint GetCursorPosition()
    {
        nint ev = CoreGraphics.CGEventCreate(0);
        if (ev == 0) return default;
        try
        {
            var p = CoreGraphics.CGEventGetLocation(ev);
            return new ScreenPoint((int)Math.Round(p.X), (int)Math.Round(p.Y));
        }
        finally { CoreFoundation.CFRelease(ev); }
    }

    /// <summary>Avalonia の TryGetPlatformHandle().Handle は NSView のことがあるので NSWindow に揃える。</summary>
    internal static nint NSWindowOf(nint handle)
    {
        if (handle == 0) return 0;
        if (RespondsTo(handle, "contentView")) return handle; // NSWindow
        return RespondsTo(handle, "window") ? Send(handle, Sel("window")) : 0;
    }
}
