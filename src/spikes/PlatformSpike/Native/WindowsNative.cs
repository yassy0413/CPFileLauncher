using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PlatformSpike.Native;

/// <summary>
/// Windows 固有: 直前の前面ウィンドウの記録と復元。
/// 製品では IWindowService の Windows 実装に入る部分。
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsNative
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const uint GA_ROOT = 2;

    public static IntPtr CaptureForeground() => GetForegroundWindow();

    public static bool RestoreForeground(IntPtr hWnd)
        => hWnd != IntPtr.Zero && SetForegroundWindow(hWnd);

    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);

    public static bool IsForeground(IntPtr hWnd) => GetForegroundWindow() == hWnd;

    /// <summary>
    /// フォアグラウンドロックを回避して前面化する。
    /// フックで入力を抑止して呼び出すと、このプロセスは「最後に入力を受けたプロセス」ではないため
    /// SetForegroundWindow が拒否され、ポップアップがアクティブにならない（→ 外クリックで Deactivated が来ない）。
    /// 前面ウィンドウのスレッドに入力キューを一時接続すると前面化が許可される。
    /// </summary>
    public static bool ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        if (SetForegroundWindow(hWnd) && IsForeground(hWnd)) return true;

        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint myThread = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != myThread && AttachThreadInput(myThread, fgThread, true);
        try
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached) AttachThreadInput(myThread, fgThread, false);
        }
        return IsForeground(hWnd);
    }

    /// <summary>
    /// カーソル下のトップレベルウィンドウのクラス名。
    /// デスクトップ(壁紙)上なら "Progman" または "WorkerW" になる想定（SPEC §4.2「デスクトップ上のみ」の判定）。
    /// </summary>
    public static string ClassNameUnderPoint(int x, int y)
    {
        var h = WindowFromPoint(new POINT { X = x, Y = y });
        if (h == IntPtr.Zero) return "(none)";
        h = GetAncestor(h, GA_ROOT);
        var sb = new System.Text.StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }
}
