using System.Runtime.Versioning;
using System.Text;
using FileLauncher.Core.Input;
using static FileLauncher.Platform.Windows.Native.User32;

namespace FileLauncher.Platform.Windows;

/// <summary>カーソル下のトップレベルウィンドウが Progman / WorkerW ならデスクトップ（PlatformSpike I で確認）。</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopDetector : IDesktopDetector
{
    public bool IsDesktopAt(ScreenPoint point)
    {
        var h = WindowFromPoint(new POINT { X = point.X, Y = point.Y });
        if (h == IntPtr.Zero) return false;
        h = GetAncestor(h, GA_ROOT);
        var sb = new StringBuilder(64);
        GetClassName(h, sb, sb.Capacity);
        var cls = sb.ToString();
        return cls is "Progman" or "WorkerW";
    }
}
