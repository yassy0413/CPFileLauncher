using System.Diagnostics;
using System.Runtime.Versioning;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Platform.MacOS.Native.CoreFoundation;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// Finder のメニュー「新規タブ」を AX で押す（SPEC §5.2「macOS: Finder のタブ」）。
/// 項目は名前ではなくショートカット（⌘T = AXMenuItemCmdChar "T"・修飾 0）で探すので UI の言語に依らない。
/// System Events を使わないので、自動化の許可は Finder の 1 件だけで済む。Finder が背面でも押せる。
/// </summary>
[SupportedOSPlatform("macos")]
internal static class FinderMenu
{
    public static bool PressNewTab(int finderPid)
    {
        var owned = new List<nint>();
        try
        {
            nint app = Accessibility.AXUIElementCreateApplication(finderPid);
            if (app == 0) return Fail("AXUIElementCreateApplication failed");
            owned.Add(app);
            Accessibility.AXUIElementSetMessagingTimeout(app, 1.0f);

            nint bar = Copy(app, "AXMenuBar", owned);
            if (bar == 0) return Fail("no AXMenuBar");
            foreach (nint top in Children(bar, owned))
            foreach (nint menu in Children(top, owned))
            foreach (nint item in Children(menu, owned))
            {
                if (ToManagedString(Copy(item, "AXMenuItemCmdChar", owned)) != "T") continue;
                if (Number(Copy(item, "AXMenuItemCmdModifiers", owned)) != 0) continue;
                nint press = CreateString("AXPress");
                owned.Add(press);
                int err = Accessibility.AXUIElementPerformAction(item, press);
                return err == Accessibility.kAXErrorSuccess || Fail($"AXPress error {err}");
            }
            return Fail("menu item ⌘T not found");
        }
        finally
        {
            foreach (nint o in owned) CFRelease(o);
        }
    }

    private static bool Fail(string message)
    {
        Trace.WriteLine($"[finder] new tab: {message}");
        return false;
    }

    /// <summary>属性値を取る（Copy 規則なので owned に積んで最後に解放）。無ければ 0。</summary>
    private static nint Copy(nint element, string attribute, List<nint> owned)
    {
        if (element == 0) return 0;
        nint key = CreateString(attribute);
        try
        {
            if (Accessibility.AXUIElementCopyAttributeValue(element, key, out nint value) != Accessibility.kAXErrorSuccess || value == 0) return 0;
            owned.Add(value);
            return value;
        }
        finally { CFRelease(key); }
    }

    /// <summary>AXChildren の各要素（配列が持っているので個別には解放しない）。</summary>
    private static IEnumerable<nint> Children(nint element, List<nint> owned)
    {
        nint array = Copy(element, "AXChildren", owned);
        if (array == 0) yield break;
        long count = CFArrayGetCount(array);
        for (long i = 0; i < count; i++) yield return CFArrayGetValueAtIndex(array, i);
    }

    private static long Number(nint value)
        => value != 0 && CFGetTypeID(value) == CFNumberGetTypeID() && CFNumberGetValue(value, kCFNumberSInt64Type, out long v) ? v : -1;
}
