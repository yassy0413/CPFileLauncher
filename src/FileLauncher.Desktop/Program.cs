using Avalonia;
using FileLauncher.Core.Storage;

namespace FileLauncher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var paths = DataPaths.Resolve(DataPaths.ExecutableDirectory(Environment.ProcessPath, AppContext.BaseDirectory));
        // 2 重起動なら既存インスタンスに「盤面を表示」を送って終了（SPEC §10.3）。Avalonia を立ち上げる前に判定する:
        // OnFrameworkInitializationCompleted の中で Shutdown() してもメインループ開始前なので効かず、2 つ目が残り続けた（2026-10-05 Windows）
        var instance = new SingleInstance(paths.Root);
        if (!instance.TryAcquire())
        {
            instance.SignalExisting();
            instance.Dispose();
            return;
        }
        Instance = instance;
        string settingsFile = paths.SettingsFile;
        // 表示言語は起動時に決める（変更は再起動で反映。SPEC §7）。--lang ja|en は確認用で、設定より優先し保存しない
        int lang = Array.IndexOf(args, "--lang");
        string language = lang >= 0 && lang + 1 < args.Length && args[lang + 1] is "ja" or "en" ? args[lang + 1] : SettingsPeek.Language(settingsFile);
        Loc.ApplyStartupLanguage(language);
        // ハードウェアアクセラレーションも起動時にしか変えられないので、Avalonia の前に設定を 1 キーだけ読む（spec/SETTINGS.md 詳細タブ）
        bool hardware = SettingsPeek.HardwareAcceleration(settingsFile);
        HardwareAccelerationAtStartup = hardware;
        var builder = BuildAvaloniaApp();
        if (!hardware)
        {
            builder = builder
                .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        }
        builder.StartWithClassicDesktopLifetime(args);
    }

    /// <summary>起動時に使った描画方式（設定画面の「今すぐ再起動」を出すかの判定用）。</summary>
    public static bool HardwareAccelerationAtStartup { get; private set; } = true;

    /// <summary>Main で取得済みの単一インスタンスのロック（App が引き継いで待ち受け・解放する）。</summary>
    public static SingleInstance? Instance { get; private set; }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // macOS: Dock に出さない（.app では Info.plist の LSUIElement も設定する。SPEC §8）
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .WithInterFont()
            .LogToTrace();
}
