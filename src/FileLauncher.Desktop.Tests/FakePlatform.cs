using FileLauncher.Core.Input;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform;

namespace FileLauncher.Desktop.Tests;

/// <summary>OS に触らない IPlatformServices。呼ばれた内容を記録する。</summary>
internal sealed class FakePlatform : IPlatformServices, IInputHookService, IHotkeyService, IMouseTriggerService,
    IIconProvider, IShellService, IWindowService, IAutoStartService, IPermissionService, IDesktopDetector
{
    public bool HookRunning { get; set; }
    public HotkeySetting? RegisteredHotkey { get; private set; }
    public Action<string, KeyModifiers>? Capture { get; private set; }
    public List<string> Launched { get; } = new();

    public IInputHookService InputHook => this;
    public IHotkeyService Hotkey => this;
    public IMouseTriggerService MouseTrigger => this;
    public IIconProvider Icons => this;
    public IShellService Shell => this;
    public IWindowService Window => this;
    public IAutoStartService AutoStart => this;
    public IPermissionService Permissions => this;
    public IDesktopDetector Desktop => this;

    /// <summary>既定は非対応（従来の Avalonia の層）。OS の層のテストでは FakeAmbientLayers を入れる。</summary>
    public IAmbientLayerService Ambient { get; set; } = AmbientLayers.Unsupported;

    // 入力フック
    public void Start() { }
    public bool IsRunning => HookRunning;
    public event Action<string>? Failed { add { } remove { } }

    // ホットキー
    public bool Register(HotkeySetting hotkey, out string? error) { RegisteredHotkey = hotkey; error = null; return true; }
    public void Unregister() => RegisteredHotkey = null;
    public void BeginCapture(Action<string, KeyModifiers> onKey) => Capture = onKey;
    public void EndCapture() => Capture = null;
    public event Action<long>? Pressed { add { } remove { } }

    // マウス
    public TriggerSettings? Configured { get; private set; }
    public void Configure(TriggerSettings settings) => Configured = settings;
    public event Action<ScreenPoint, long>? Triggered { add { } remove { } }
    public event Action<ScreenPoint>? PassedThroughDown { add { } remove { } }
    public event Action<ScreenPoint, long, int>? WheelTriggered { add { } remove { } }
    public event Action<ScreenPoint>? PassedThroughUp { add { } remove { } }

    // アイコン・シェル
    public Task<IconPixels?> GetIconAsync(LauncherItem item, int sizePx, bool allowThumbnail, CancellationToken cancellationToken = default) =>
        Task.FromResult<IconPixels?>(null);
    public LaunchResult Launch(LauncherItem item, IReadOnlyList<string>? droppedPaths = null, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        Launched.Add(item.Target);
        LastFolderTarget = folderTarget;
        return LaunchResult.Ok;
    }
    public LaunchResult RevealInFileManager(string path, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        Revealed.Add((path, folderTarget));
        return LaunchResult.Ok;
    }
    public FolderOpenTarget? LastFolderTarget { get; private set; }
    public List<(string Path, FolderOpenTarget Target)> Revealed { get; } = new();
    public LaunchResult OpenTerminal(string directory)
    {
        OpenedTerminals.Add(directory);
        return OpenTerminalResult;
    }
    public List<string> OpenedTerminals { get; } = new();
    public LaunchResult OpenTerminalResult { get; set; } = LaunchResult.Ok;
    public ShortcutInfo? ReadShortcut(string path) => null;

    // ウィンドウ
    public bool UsesLogicalScreenCoordinates => false;
    public void ConfigureBoardWindow(nint windowHandle, DisplayMode mode) { }
    public void SetZOrder(nint windowHandle, ZOrder order) { }
    public nint CaptureForeground() => 0;
    public bool ReducedMotion { get; set; }
    bool IWindowService.PrefersReducedMotion => ReducedMotion;
    public bool BringToForeground(nint windowHandle) => true;
    public bool RestoreForeground(nint previous) => true;
    public ScreenPoint GetCursorPosition() => new(100, 100);

    // 自動起動・権限・デスクトップ
    public bool IsEnabled { get; private set; }
    public void SetEnabled(bool enabled, string executablePath) => IsEnabled = enabled;
    public PermissionState InputMonitoring => PermissionState.Granted;
    public PermissionState Accessibility => PermissionState.Granted;
    public void RequestAccessibility() { }
    public void OpenSystemSettings() { }
    public bool IsDesktopAt(ScreenPoint point) => false;

    public void Dispose() { }
}
