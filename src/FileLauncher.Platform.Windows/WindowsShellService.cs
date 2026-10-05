using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform.Windows.Native;

namespace FileLauncher.Platform.Windows;

/// <summary>起動・フォルダを開く・.lnk 読み取り（SPEC §5.2）。Drop-to-Open のコピー/移動は Core の FileTransfer。</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsShellService : IShellService
{
    private const int ErrorCancelled = 1223; // UAC で「いいえ」

    private readonly StaWorker _sta;
    private readonly ExplorerTabs _explorer;

    public WindowsShellService(StaWorker sta, ExplorerTabs explorer)
    {
        _sta = sta;
        _explorer = explorer;
    }

    public LaunchResult Launch(LauncherItem item, IReadOnlyList<string>? droppedPaths = null, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        var psi = new ProcessStartInfo { UseShellExecute = true };
        string target = LaunchArgs.ExpandPath(item.Target);

        switch (item.Kind)
        {
            case ItemKind.Url:
                psi.FileName = item.Target;
                break;

            case ItemKind.Command:
                // コマンド（シェル実行）: cmd /c で実行する
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                psi.Arguments = $"/c \"{target} {LaunchArgs.Build(item.Args, droppedPaths)}\"";
                break;

            default:
                if (!IsNetworkPath(target) && !File.Exists(target) && !Directory.Exists(target))
                    return LaunchResult.Fail(LaunchFailure.NotFound, target);
                if (item.Kind == ItemKind.Folder && folderTarget != FolderOpenTarget.System)
                {
                    // 既存のタブ / 新しいウィンドウ（SPEC §5.2）。ExplorerSTA で続け、ここでは待たない
                    _explorer.OpenFolder(target, null, folderTarget, item.LaunchMode);
                    return LaunchResult.Ok;
                }
                psi.FileName = target;
                if (item.Kind != ItemKind.Folder)
                {
                    psi.Arguments = LaunchArgs.Build(LaunchArgs.ExpandPath(item.Args), droppedPaths);
                    psi.WorkingDirectory = item.WorkingDir is { Length: > 0 } wd
                        ? LaunchArgs.ExpandPath(wd)
                        : Path.GetDirectoryName(target) ?? "";
                }
                break;
        }

        psi.WindowStyle = item.LaunchMode switch
        {
            LaunchMode.Minimized => ProcessWindowStyle.Minimized,
            LaunchMode.Maximized => ProcessWindowStyle.Maximized,
            _ => ProcessWindowStyle.Normal,
        };
        if (item.RunAsAdmin && item.Kind is ItemKind.App or ItemKind.File or ItemKind.Command) psi.Verb = "runas";

        return Start(psi);
    }

    public LaunchResult RevealInFileManager(string path, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        path = LaunchArgs.ExpandPath(path);
        if (!File.Exists(path) && !Directory.Exists(path)) return LaunchResult.Fail(LaunchFailure.NotFound, path);
        if (folderTarget != FolderOpenTarget.System && Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) is { } parent)
        {
            // 親フォルダを開いて path を選択する（フォルダ自身が対象でも同じ）
            _explorer.OpenFolder(parent, path, folderTarget, LaunchMode.Normal);
            return LaunchResult.Ok;
        }
        return Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    public ShortcutInfo? ReadShortcut(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var link = _sta.Invoke(() => ShellIcons.ReadLink(path));
            return new ShortcutInfo(link.Target, link.Arguments, link.WorkingDirectory);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static LaunchResult Start(ProcessStartInfo psi)
    {
        try
        {
            using var _ = Process.Start(psi);
            return LaunchResult.Ok;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return LaunchResult.Fail(LaunchFailure.Cancelled);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return LaunchResult.Fail(LaunchFailure.OsError, $"{ex.Message}: {psi.FileName}");
        }
    }

    private static bool IsNetworkPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);
}
