namespace FileLauncher.Core.Items;

/// <summary>起動時の文字列展開（SPEC §5.1, §5.3）。</summary>
public static class LaunchArgs
{
    /// <summary>
    /// 引数に Drop-to-Open のパスを入れる。%1 / $1 があればそこへ（"%1" と引用符付きで書かれていても二重にしない）、
    /// 無ければ末尾に足す。パスはそれぞれ引用符で囲み空白区切り。
    /// </summary>
    public static string Build(string args, IReadOnlyList<string>? droppedPaths)
    {
        args ??= string.Empty;
        string paths = droppedPaths is { Count: > 0 } ? string.Join(" ", droppedPaths.Select(p => $"\"{p}\"")) : string.Empty;

        if (args.Contains("%1") || args.Contains("$1"))
        {
            return args
                .Replace("\"%1\"", paths).Replace("%1", paths)
                .Replace("\"$1\"", paths).Replace("$1", paths)
                .Trim();
        }
        return paths.Length == 0 ? args : $"{args} {paths}".Trim();
    }

    /// <summary>環境変数（%USERPROFILE% 等）と ~ / $HOME を展開する。保存データは展開しないまま持ち、起動・アイコン取得時にだけ使う。</summary>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path == "~") path = home;
        else if (path.StartsWith("~/") || path.StartsWith("~\\")) path = home + path[1..];
        path = path.Replace("$HOME", home);
        return Environment.ExpandEnvironmentVariables(path);
    }
}
