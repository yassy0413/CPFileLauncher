using System.Runtime.Versioning;
using FileLauncher.Core.Input;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Platform.MacOS.Native.Accessibility;
using static FileLauncher.Platform.MacOS.Native.CoreFoundation;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// カーソル下がデスクトップか（SPEC §4.2）。CGWindowList の前面順は Dock の全画面透明ウィンドウ等が常に手前にあって使えない
/// （PlatformSpike I）。AX でクリックが実際に届く要素を取り、Finder の要素で、その所属ウィンドウの role が
/// AXScrollArea（通常のウィンドウは AXWindow）ならデスクトップとみなす。デスクトップ上のアイコンも含む（Win の Progman と同じ扱い）。
/// description（"desktop"）はローカライズされうるので使わない。
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacDesktopDetector : IDesktopDetector
{
    private readonly nint _systemWide = AXUIElementCreateSystemWide();
    private readonly nint _attrRole = CreateString("AXRole");
    private readonly nint _attrWindow = CreateString("AXWindow");
    private int _finderPid;

    public MacDesktopDetector()
    {
        // フックのコールバック内で呼ばれるので、応答しないアプリで長く止まらないようにする
        AXUIElementSetMessagingTimeout(_systemWide, 0.1f);
    }

    public bool IsDesktopAt(ScreenPoint point)
    {
        if (AXUIElementCopyElementAtPosition(_systemWide, point.X, point.Y, out nint element) != kAXErrorSuccess || element == 0)
            return false;
        try
        {
            if (AXUIElementGetPid(element, out int pid) != kAXErrorSuccess || !IsFinder(pid)) return false;
            if (AXUIElementCopyAttributeValue(element, _attrWindow, out nint window) != kAXErrorSuccess || window == 0)
                return false;
            try
            {
                return RoleOf(window) == "AXScrollArea";
            }
            finally { CFRelease(window); }
        }
        finally { CFRelease(element); }
    }

    private string? RoleOf(nint element)
    {
        if (AXUIElementCopyAttributeValue(element, _attrRole, out nint role) != kAXErrorSuccess || role == 0) return null;
        try { return ToManagedString(role); }
        finally { CFRelease(role); }
    }

    private bool IsFinder(int pid)
    {
        if (pid == _finderPid && pid != 0) return true;
        using var _ = AutoreleasePool.Push();
        nint app = ObjC.SendInt(ObjC.Class("NSRunningApplication"), ObjC.Sel("runningApplicationWithProcessIdentifier:"), pid);
        bool finder = app != 0 && ObjC.ToManagedString(ObjC.Send(app, ObjC.Sel("bundleIdentifier"))) == "com.apple.finder";
        if (finder) _finderPid = pid; // Finder が再起動したら pid が変わるので一致しなければ毎回引き直す
        return finder;
    }
}
