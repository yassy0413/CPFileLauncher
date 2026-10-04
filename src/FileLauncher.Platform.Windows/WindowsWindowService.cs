using System.Runtime.Versioning;
using FileLauncher.Core.Input;
using FileLauncher.Core.Model;
using static FileLauncher.Platform.Windows.Native.User32;

namespace FileLauncher.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsWindowService : IWindowService
{
    /// <summary>「アニメーション効果」（設定 → アクセシビリティ → 視覚効果）が OFF なら true。</summary>
    public bool PrefersReducedMotion =>
        SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out bool on, 0) && !on;

    public bool UsesLogicalScreenCoordinates => false;

    public void ConfigureBoardWindow(nint windowHandle, DisplayMode mode)
    {
        // ツールウィンドウにすると Alt+Tab にも出ない（Avalonia の ShowInTaskbar=false だけだと Alt+Tab には出る）
        long ex = GetWindowLongPtr(windowHandle, GWL_EXSTYLE).ToInt64();
        ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, new IntPtr(ex));
        SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    public void SetZOrder(nint windowHandle, ZOrder order)
    {
        var after = order switch
        {
            ZOrder.Topmost => HWND_TOPMOST,
            ZOrder.Bottommost => HWND_BOTTOM,
            _ => HWND_NOTOPMOST,
        };
        SetWindowPos(windowHandle, after, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public nint CaptureForeground() => GetForegroundWindow();

    /// <summary>
    /// フックで入力を抑止して呼び出すと、このプロセスは「最後に入力を受けたプロセス」ではないため
    /// SetForegroundWindow が拒否される（フォアグラウンドロック）。前面ウィンドウのスレッドに
    /// 入力キューを一時接続すると前面化が許可される（SPEC §3.3、PlatformSpike F で確認）。
    /// </summary>
    public bool BringToForeground(nint windowHandle)
    {
        if (windowHandle == 0) return false;
        if (GetForegroundWindow() == windowHandle) return true;
        if (SetForegroundWindow(windowHandle) && GetForegroundWindow() == windowHandle) return true;

        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint myThread = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != myThread && AttachThreadInput(myThread, fgThread, true);
        try
        {
            BringWindowToTop(windowHandle);
            SetForegroundWindow(windowHandle);
        }
        finally
        {
            if (attached) AttachThreadInput(myThread, fgThread, false);
        }
        return GetForegroundWindow() == windowHandle;
    }

    public bool RestoreForeground(nint previous) =>
        previous != 0 && IsWindow(previous) && SetForegroundWindow(previous);

    public ScreenPoint GetCursorPosition() =>
        GetCursorPos(out var p) ? new ScreenPoint(p.X, p.Y) : default;
}
