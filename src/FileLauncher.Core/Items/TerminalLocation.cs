using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>
/// 「ターミナルで開く」のフォルダ（SPEC §6.3「ターミナルで開く」）。文字列処理だけで I/O はしない（メニュー構築時に呼ぶため）。
/// .lnk / エイリアスは LinkPath ではなく実体（Target）側。存在確認は Platform が選択時に行う。
/// </summary>
public static class TerminalLocation
{
    /// <summary>開くフォルダ。項目を出さないアイテム（URL・作業フォルダの無いコマンド・親が取れない）は null。</summary>
    public static string? For(LauncherItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Url:
                return null;
            case ItemKind.Command:
                return NonEmpty(item.WorkingDir);
            case ItemKind.Folder:
                if (string.IsNullOrWhiteSpace(item.Target)) return null;
                // ルート（C:\ / /）は .NET がそのまま返す
                return Path.TrimEndingDirectorySeparator(LaunchArgs.ExpandPath(item.Target));
            default: // File / App
                if (NonEmpty(item.WorkingDir) is { } wd) return wd;
                if (string.IsNullOrWhiteSpace(item.Target)) return null;
                string? parent = Path.GetDirectoryName(LaunchArgs.ExpandPath(item.Target));
                return string.IsNullOrEmpty(parent) ? null : parent;
        }
    }

    static string? NonEmpty(string? dir)
        => string.IsNullOrWhiteSpace(dir) ? null : LaunchArgs.ExpandPath(dir);
}
