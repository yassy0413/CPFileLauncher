using System.Runtime.Versioning;
using FileLauncher.Core.Model;
using FileLauncher.Platform.Input;
using Microsoft.Win32;

namespace FileLauncher.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServices : IPlatformServices
{
    private readonly SharpHookInputService _input;
    private readonly StaWorker _sta = new("ShellSTA");
    // Explorer のタブ操作はアイコン取得と別のスレッド（タブの出現待ちでアイコン取得を止めない。SPEC §10.3）
    private readonly StaWorker _explorerSta = new("ExplorerSTA");

    public WindowsPlatformServices()
    {
        Desktop = new WindowsDesktopDetector();
        _input = new SharpHookInputService(Desktop.IsDesktopAt);
        Icons = new WindowsIconProvider(_sta);
        Shell = new WindowsShellService(_sta, new ExplorerTabs(_explorerSta));
    }

    public IInputHookService InputHook => _input;
    public IHotkeyService Hotkey => _input;
    public IMouseTriggerService MouseTrigger => _input;
    public IIconProvider Icons { get; }
    public IShellService Shell { get; }
    public IWindowService Window { get; } = new WindowsWindowService();
    public IAutoStartService AutoStart { get; } = new WindowsAutoStartService();
    public IPermissionService Permissions { get; } = new WindowsPermissionService();
    public IDesktopDetector Desktop { get; }

    public void Dispose()
    {
        _input.Dispose();
        _sta.Dispose();
        _explorerSta.Dispose();
    }
}

/// <summary>Windows には入力監視の権限が無いので常に許可済み。</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPermissionService : IPermissionService
{
    public PermissionState InputMonitoring => PermissionState.Granted;
    public PermissionState Accessibility => PermissionState.Granted;
    public void RequestAccessibility() { }
    public void OpenSystemSettings() { }
}

/// <summary>ログイン時自動起動: HKCU\Software\Microsoft\Windows\CurrentVersion\Run（SPEC §10.3）。</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CPFileLauncher";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{executablePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
