using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace FileLauncher.Platform.Windows.Native;

/// <summary>
/// Explorer のウィンドウとタブの列挙（SPEC §5.2「フォルダを開く先」、§10.3）。
/// 新しいタブは非公開の WM_COMMAND 0xA21B で開く（ExplorerTabUtility 等と同じ手口。Windows の更新で壊れうる）。
/// COM は呼び出し元の STA スレッド（ExplorerSTA）で使う。
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExplorerWindows
{
    public const string WindowClass = "CabinetWClass";
    public const string TabClass = "ShellTabWindowClass";
    private const uint WM_COMMAND = 0x0111;
    private const int CommandNewTab = 0xA21B;
    private const int DWMWA_CLOAKED = 14;
    private const int SW_RESTORE = 9;

    /// <summary>開いているタブ 1 つ。Browser は IWebBrowser2（dynamic で呼ぶ）。</summary>
    public sealed record Tab(nint Window, nint TabHandle, object Browser)
    {
        public string? Path
        {
            get
            {
                try { return (string)((dynamic)Browser).Document.Folder.Self.Path; }
                catch (Exception) { return null; } // 移動中・特殊フォルダ
            }
        }
    }

    /// <summary>CabinetWClass を Z オーダー順（手前から）に。</summary>
    public static List<nint> TopWindows()
    {
        var list = new List<nint>();
        EnumWindows((h, _) => { if (ClassName(h) == WindowClass) list.Add(h); return true; }, 0);
        return list;
    }

    public static bool IsVisible(nint h) => IsWindowVisible(h);
    public static bool IsMinimized(nint h) => IsIconic(h);
    public static void Restore(nint h) => ShowWindow(h, SW_RESTORE);

    /// <summary>別の仮想デスクトップ等で隠されている（cloak）か。</summary>
    public static bool IsCloaked(nint h) => DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    /// <summary>表示中のタブ（先頭の子）。タブの無い Windows（10 など）では 0。</summary>
    public static nint ActiveTab(nint window) => FindWindowEx(window, 0, TabClass, null);

    public static int TabCount(nint window)
    {
        int n = 0;
        for (nint c = FindWindowEx(window, 0, TabClass, null); c != 0; c = FindWindowEx(window, c, TabClass, null)) n++;
        return n;
    }

    public static bool RequestNewTab(nint tab) => PostMessage(tab, WM_COMMAND, CommandNewTab, 0);

    /// <summary>IShellWindows に登録されている Explorer のタブ（IE 等は除く）。</summary>
    public static List<Tab> Tabs()
    {
        var list = new List<Tab>();
        var type = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
        if (type is null) return list;
        dynamic windows = Activator.CreateInstance(type)!;
        int count = windows.Count;
        for (int i = 0; i < count; i++)
        {
            try
            {
                object? browser = windows.Item(i);
                if (browser is null) continue;
                nint window = (nint)(long)((dynamic)browser).HWND;
                if (ClassName(window) != WindowClass) continue;
                list.Add(new Tab(window, TabHandle(browser), browser));
            }
            catch (Exception) { } // 閉じかけのウィンドウ
        }
        return list;
    }

    /// <summary>IWebBrowser2 → IShellBrowser → タブのウィンドウ（ShellTabWindowClass）。</summary>
    private static nint TabHandle(object browser)
    {
        if (browser is not IServiceProvider sp) return 0;
        Guid sid = SID_STopLevelBrowser, iid = IID_IShellBrowser;
        if (sp.QueryService(ref sid, ref iid, out var ptr) != 0 || ptr == 0) return 0;
        try
        {
            var sb = (IShellBrowser)Marshal.GetObjectForIUnknown(ptr);
            return sb.GetWindow(out var h) == 0 ? h : 0;
        }
        finally { Marshal.Release(ptr); }
    }

    /// <summary>タブの中で name を選択する（他の選択は外し、見える位置へ、フォーカス）。</summary>
    public static bool SelectItem(Tab tab, string name)
    {
        try
        {
            dynamic doc = ((dynamic)tab.Browser).Document;
            dynamic? item = doc.Folder.ParseName(name);
            if (item is null) return false;
            doc.SelectItem(item, SVSIF_SELECT | SVSIF_DESELECTOTHERS | SVSIF_ENSUREVISIBLE | SVSIF_FOCUSED);
            return true;
        }
        catch (Exception) { return false; }
    }

    public static void Navigate(Tab tab, string folder) => ((dynamic)tab.Browser).Navigate2(folder);

    private const int SVSIF_SELECT = 1, SVSIF_DESELECTOTHERS = 4, SVSIF_ENSUREVISIBLE = 8, SVSIF_FOCUSED = 16;
    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    private static string ClassName(nint h)
    {
        var sb = new StringBuilder(64);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string cls, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid riid, out nint obj);
    }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        [PreserveSig] int GetWindow(out nint hwnd); // IOleWindow の先頭。ここから先のメソッドは使わない
    }
}
