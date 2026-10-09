using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FileLauncher.Core.Model;
using FileLauncher.Platform;

namespace FileLauncher.App;

/// <summary>「FileLauncher について」（SPEC §6.3）。バージョン・データフォルダ・同梱フォントのライセンス。製品名は仮のまま（§13.2）。</summary>
internal static class AboutWindow
{
    private static Window? _open; // 開いていれば前に出すだけ

    internal static Window Show(IShellService shell, string dataRoot, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        if (_open is { } existing) { existing.Activate(); return existing; }

        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";
        int plus = version.IndexOf('+'); // ビルドメタデータ（コミットハッシュ）は省く
        if (plus >= 0) version = version[..plus];
#if DEBUG
        version += " (Debug)";
#endif

        var openData = new Button { Content = Strings.Common_Open };
        openData.Click += (_, _) =>
        {
            var r = shell.Launch(new LauncherItem { Name = "data", Target = dataRoot, Kind = ItemKind.Folder }, null, folderTarget);
            if (!r.Success) Toast.Show(Strings.FormatAbout_OpenDataFailed(Loc.LaunchError(r)));
        };
        var ok = Dialogs.ActionButton(Strings.Common_OK, isDefault: true);
        ok.HorizontalAlignment = HorizontalAlignment.Right;

        var window = new ChromeWindow
        {
            Title = Strings.About_Title,
            TagCode = "INFO",
            BodyWidth = 520,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Body = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    Themed.Glow(new TextBlock { Text = AppInfo.ProductName, FontWeight = FontWeight.Bold, FontSize = 18 }),
                    Themed.Note(new TextBlock { Text = AppInfo.Subtitle }),
                    new TextBlock { Text = Strings.FormatAbout_Version(version) },
                    new TextBlock { Text = Strings.About_DataFolder, FontWeight = FontWeight.SemiBold },
                    new DockPanel
                    {
                        Children =
                        {
                            Dock(openData),
                            new TextBox { Text = dataRoot, IsReadOnly = true, Margin = new Thickness(0, 0, 8, 0) },
                        },
                    },
                    new Expander
                    {
                        Header = Strings.About_FontLicense,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Content = new ScrollViewer
                        {
                            MaxHeight = 260,
                            Content = new SelectableTextBlock { Text = ReadLicense(), TextWrapping = TextWrapping.Wrap, FontSize = 12 },
                        },
                    },
                    ok,
                },
            },
        };
        ok.Click += (_, _) => window.Close();
        window.Closed += (_, _) => _open = null;
        _open = window;
        window.Show();
        return window;
    }

    private static Control Dock(Control c)
    {
        DockPanel.SetDock(c, Avalonia.Controls.Dock.Right);
        return c;
    }

    private static string ReadLicense()
    {
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts");
            return string.Join("\n\n―――\n\n", new[] { "OFL-ChakraPetch.txt", "OFL-ShareTechMono.txt" }.Select(f => File.ReadAllText(Path.Combine(dir, f))));
        }
        catch (IOException) { return Strings.About_LicenseMissing; }
        catch (UnauthorizedAccessException) { return Strings.About_LicenseUnreadable; }
    }
}
