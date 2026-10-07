using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Platform.MacOS.Native.ObjC;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// 起動・Finder で表示・エイリアス解決（SPEC §5.2, §10.3）。Drop-to-Open のコピー/移動は Core の FileTransfer。
/// 起動は /usr/bin/open に任せ、終了コード ≠ 0 を失敗とする。起動方法・管理者実行は macOS では無視（SPEC §5.1）。
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacShellService : IShellService
{
    private const string Open = "/usr/bin/open";

    /// <summary>open が失敗を返すのを待つ上限。成功時は 100ms 前後で終わる。</summary>
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(2);

    public LaunchResult Launch(LauncherItem item, IReadOnlyList<string>? droppedPaths = null, FolderOpenTarget folderTarget = FolderOpenTarget.System) // folderTarget は macOS では無視（SPEC §5.2）
    {
        string target = LaunchArgs.ExpandPath(item.Target);
        bool hasDrops = droppedPaths is { Count: > 0 };

        switch (item.Kind)
        {
            case ItemKind.Url:
                return RunOpen([item.Target]);

            case ItemKind.Command:
                return Start(new ProcessStartInfo("/bin/zsh")
                {
                    ArgumentList = { "-lc", $"{target} {LaunchArgs.Build(LaunchArgs.ExpandPath(item.Args), droppedPaths)}".Trim() },
                    WorkingDirectory = item.WorkingDir is { Length: > 0 } wd ? LaunchArgs.ExpandPath(wd) : Home,
                });
        }

        if (!File.Exists(target) && !Directory.Exists(target)) return LaunchResult.Fail(LaunchFailure.NotFound, target);

        if (item.Kind == ItemKind.App && IsBundle(target))
        {
            var args = new List<string> { "-a", target };
            string extra = LaunchArgs.ExpandPath(item.Args ?? "");
            if (hasDrops && !extra.Contains("%1") && !extra.Contains("$1"))
            {
                // Drop-to-Open: ドロップしたファイルをそのアプリで開く（引数より優先。open は両方は渡せない）
                args.AddRange(droppedPaths!);
            }
            else if (LaunchArgs.Build(extra, droppedPaths) is { Length: > 0 } built)
            {
                args.Add("--args");
                args.AddRange(SplitArgs(built));
            }
            return RunOpen(args);
        }

        if (item.Kind == ItemKind.App && !Directory.Exists(target))
        {
            // 実行ファイル・スクリプト（.app でないアプリ）: 直接起動する
            return Start(new ProcessStartInfo(target)
            {
                Arguments = LaunchArgs.Build(LaunchArgs.ExpandPath(item.Args ?? ""), droppedPaths),
                WorkingDirectory = item.WorkingDir is { Length: > 0 } wd2 ? LaunchArgs.ExpandPath(wd2) : Path.GetDirectoryName(target) ?? Home,
            });
        }

        // ファイル・フォルダ: 既定のアプリ / Finder で開く（open はファイルに引数を渡せないので Args は無視。SPEC §5.2）
        return RunOpen([target]);
    }

    public LaunchResult RevealInFileManager(string path, FolderOpenTarget folderTarget = FolderOpenTarget.System)
    {
        path = LaunchArgs.ExpandPath(path);
        if (!File.Exists(path) && !Directory.Exists(path)) return LaunchResult.Fail(LaunchFailure.NotFound, path);
        return RunOpen(["-R", path]);
    }

    public LaunchResult OpenTerminal(string directory)
    {
        if (!Directory.Exists(directory)) return LaunchResult.Fail(LaunchFailure.NotFound, directory);
        return RunOpen(["-a", "Terminal", directory]);
    }

    /// <summary>Finder エイリアスなら解決先を返す。シンボリックリンクは解決しない（SPEC §5.1）。</summary>
    public ShortcutInfo? ReadShortcut(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).LinkTarget is not null) return null;
            using var pool = AutoreleasePool.Push();
            nint url = FileUrl(path);
            nint key = NSString("NSURLIsAliasFileKey");
            if (!SendRetBool(url, Sel("getResourceValue:forKey:error:"), out nint value, key, out _) || value == 0) return null;
            if (!SendRetBool(value, Sel("boolValue"))) return null;

            // NSURLBookmarkResolutionWithoutUI = 1 << 8（解決できなくてもダイアログを出さない）
            nint resolved = SendResolveAlias(Class("NSURL"), Sel("URLByResolvingAliasFileAtURL:options:error:"), url, 1 << 8, out _);
            string? target = resolved == 0 ? null : ToManagedString(Send(resolved, Sel("path")));
            return string.IsNullOrEmpty(target) ? null : new ShortcutInfo(target, "", "");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static LaunchResult RunOpen(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(Open) { RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return LaunchResult.Fail(LaunchFailure.LauncherUnavailable, Open);
            if (!p.WaitForExit(OpenTimeout)) return LaunchResult.Ok; // まだ動いている = 起動処理中とみなす
            if (p.ExitCode == 0) return LaunchResult.Ok;
            string err = p.StandardError.ReadToEnd().Trim();
            return LaunchResult.Fail(LaunchFailure.OsError, err.Length > 0 ? err : $"open exit code {p.ExitCode}");
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(LaunchFailure.OsError, ex.Message);
        }
    }

    private static LaunchResult Start(ProcessStartInfo psi)
    {
        psi.UseShellExecute = false;
        try
        {
            using var _ = Process.Start(psi);
            return LaunchResult.Ok;
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(LaunchFailure.OsError, $"{ex.Message}: {psi.FileName}");
        }
    }

    private static bool IsBundle(string path) =>
        Directory.Exists(path) && path.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase);

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>引用符（"..."）を考慮して空白で分ける。LaunchArgs.Build がパスを引用符で囲むのに合わせる。</summary>
    internal static List<string> SplitArgs(string s)
    {
        var result = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false, any = false;
        foreach (char c in s)
        {
            if (c == '"') { quoted = !quoted; any = true; }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (any) { result.Add(cur.ToString()); cur.Clear(); any = false; }
            }
            else { cur.Append(c); any = true; }
        }
        if (any) result.Add(cur.ToString());
        return result;
    }
}
