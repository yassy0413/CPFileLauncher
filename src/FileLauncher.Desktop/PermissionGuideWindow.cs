using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FileLauncher.Platform;

namespace FileLauncher.App;

/// <summary>
/// macOS の権限ガイド（SPEC §4.3）。ユーザーがメニューの「権限の設定…」を選んだときだけ開く（自動では開かない）。
/// 開いている間は 1 秒ごとに状態を表示し直す。許可後のフック再開は App 側のポーリングが行う。
/// </summary>
internal sealed class PermissionGuideWindow : ChromeWindow
{
    private readonly IPlatformServices _platform;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _automation = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public PermissionGuideWindow(IPlatformServices platform, Action retry, Action restart)
    {
        _platform = platform;
        Title = AppInfo.TitlePrefix + Strings.Permission_Title;
        TagCode = "AUTH";
        BodyWidth = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Button MakeButton(string text, Action onClick)
        {
            var b = Dialogs.ActionButton(text);
            b.MinWidth = 120;
            b.Click += (_, _) => onClick();
            return b;
        }

        Body = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Strings.Permission_Heading, FontWeight = FontWeight.Bold, FontSize = 15 },
                new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    // .app ではなくターミナルから起動しているときは、権限が付くのはターミナル（CLAUDE.md）
                    Text = Strings.Permission_Steps + (IsInsideAppBundle() ? "" : "\n" + Strings.Permission_TerminalNote),
                },
                _status,
                // 自動化（Finder の制御）は任意の権限（SPEC §4.3「自動化」）。フォルダを Finder のタブで開くのに使う
                _automation,
                Themed.Foreground(new TextBlock { Text = Strings.Permission_AutomationNote, TextWrapping = TextWrapping.Wrap }, "FlTextMuted"),
                new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children =
                    {
                        MakeButton(Strings.Permission_Request, platform.Permissions.RequestAccessibility),
                        MakeButton(Strings.Permission_OpenSettings, platform.Permissions.OpenSystemSettings),
                        MakeButton(Strings.Permission_OpenAutomationSettings, platform.Permissions.OpenAutomationSettings),
                        MakeButton(Strings.Permission_Retry, retry),
                        MakeButton(Strings.Common_Restart, restart),
                        MakeButton(Strings.Common_Close, Close),
                    },
                },
            },
        };
        foreach (var b in ((WrapPanel)((StackPanel)Body!).Children[^1]).Children.OfType<Button>()) b.Margin = new Thickness(6, 0, 0, 6);

        _timer.Tick += (_, _) => Refresh();
        Opened += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var p = _platform.Permissions;
        string ax = p.Accessibility == PermissionState.Granted ? Strings.Permission_Granted : Strings.Permission_NotGranted;
        string hook = _platform.InputHook.IsRunning ? Strings.Permission_HookOn : Strings.Permission_HookOff;
        _status.Text = Strings.FormatPermission_Status(ax, hook);
        Themed.Foreground(_status, _platform.InputHook.IsRunning ? "FlSuccess" : "FlError");

        var automation = p.Automation;
        _automation.Text = Strings.FormatPermission_AutomationStatus(automation switch
        {
            PermissionState.Granted => Strings.Permission_Granted,
            PermissionState.Denied => Strings.Permission_NotGranted,
            _ => Strings.Permission_NotAsked,
        });
        // 任意の権限なので未許可でも赤にしない
        Themed.Foreground(_automation, automation == PermissionState.Granted ? "FlSuccess" : "FlTextMuted");
    }

    private static bool IsInsideAppBundle() =>
        Environment.ProcessPath?.Contains(".app/Contents/MacOS/", StringComparison.Ordinal) == true;
}
