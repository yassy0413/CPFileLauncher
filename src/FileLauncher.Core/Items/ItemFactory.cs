using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>.lnk（Win）/ エイリアス（Mac）を読んだ結果。Target が空ならシェル項目へのリンク（エクスプローラー等）。</summary>
public sealed record ShortcutInfo(string Target, string Arguments, string WorkingDirectory);

/// <summary>
/// ドロップされたパス・テキストからアイテムを作る（SPEC §5.1, §6.1）。
/// .lnk はリンク先を解決して登録し、元の .lnk は LinkPath に保持する（アイコンは .lnk 自身から取る。SPEC §5.4）。
/// </summary>
public sealed class ItemFactory
{
    /// <summary>「アプリケーション」として扱う拡張子（それ以外のファイルは「ファイル」）。</summary>
    private static readonly HashSet<string> AppExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msc", ".cpl", ".appref-ms",
    };

    private static readonly string[] UrlSchemes = ["http", "https", "ftp", "mailto"];

    private readonly Func<string, ShortcutInfo?> _readShortcut;

    /// <param name="readShortcut">ショートカットを読む（IShellService.ReadShortcut）。ショートカットでなければ null。</param>
    public ItemFactory(Func<string, ShortcutInfo?> readShortcut) => _readShortcut = readShortcut;

    /// <summary>ファイル・フォルダのパスから。存在しなければ null。</summary>
    public LauncherItem? FromPath(string path)
    {
        path = path.Trim().Trim('"');
        if (path.Length == 0) return null;

        if (Directory.Exists(path))
        {
            // Finder からは "/Applications/Safari.app/" のように末尾区切り付きで届く（SPEC §5.1）。ルート "/" や "C:\" は残す
            path = TrimTrailingSeparator(path);
            return new LauncherItem
            {
                Kind = KindOf(path),
                Target = path,
                Name = DisplayNameOf(path),
            };
        }
        if (!File.Exists(path)) return null;

        // ショートカットか（Win: .lnk、Mac: Finder エイリアス）は Platform が判定する。違えば null
        if (_readShortcut(path) is { } link)
        {
            var item = new LauncherItem { Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } ln ? ln : Path.GetFileName(path), LinkPath = path };
            if (string.IsNullOrEmpty(link.Target))
            {
                // シェル項目へのリンク（エクスプローラー、ストアアプリ等）: .lnk 自体を起動する
                item.Kind = ItemKind.App;
                item.Target = path;
                return item;
            }
            item.Target = Directory.Exists(link.Target) ? TrimTrailingSeparator(link.Target) : link.Target;
            item.Kind = KindOf(item.Target);
            item.Args = link.Arguments;
            item.WorkingDir = string.IsNullOrEmpty(link.WorkingDirectory) ? null : link.WorkingDirectory;
            return item;
        }

        return new LauncherItem
        {
            Kind = KindOf(path),
            Target = path,
            Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n ? n : Path.GetFileName(path),
        };
    }

    /// <summary>テキストから（ブラウザからの URL / リンク、パス文字列）。解釈できなければ null。</summary>
    public LauncherItem? FromText(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is null) return null;
        line = line.Trim('"');

        if (Uri.TryCreate(line, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) return FromPath(uri.LocalPath);
            if (UrlSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            {
                return new LauncherItem
                {
                    Kind = ItemKind.Url,
                    Target = line,
                    Name = uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase) ? uri.UserInfo + "@" + uri.Host : uri.Host,
                };
            }
        }
        return FromPath(line);
    }

    private static ItemKind KindOf(string path)
    {
        if (Directory.Exists(path)) return IsMacAppBundle(path) ? ItemKind.App : ItemKind.Folder;
        return AppExtensions.Contains(Path.GetExtension(path)) ? ItemKind.App : ItemKind.File;
    }

    private static bool IsMacAppBundle(string path) =>
        path.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase);

    private static string TrimTrailingSeparator(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        if (trimmed.Length == 0) return path;                       // "/"
        if (trimmed.Length == 2 && trimmed[1] == ':') return path;   // "C:\"
        return trimmed;
    }

    /// <summary>
    /// 表示名の既定（編集ダイアログで空のまま OK したとき。SPEC §6.4）: フォルダはフォルダ名（.app は拡張子なし）、
    /// ファイル・アプリは拡張子なしのファイル名、URL はホスト名（mailto はアドレス）、コマンドは先頭の語。
    /// </summary>
    public static string DefaultName(ItemKind kind, string target)
    {
        target = (target ?? "").Trim().Trim('"');
        if (target.Length == 0) return "";
        switch (kind)
        {
            case ItemKind.Url when Uri.TryCreate(target, UriKind.Absolute, out var uri):
                return uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase) ? uri.UserInfo + "@" + uri.Host
                    : uri.Host.Length > 0 ? uri.Host : target;
            case ItemKind.Command:
                return target.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            case ItemKind.Folder:
                return DisplayNameOf(target);
            default:
                if (target.TrimEnd('/', '\\').EndsWith(".app", StringComparison.OrdinalIgnoreCase)) return DisplayNameOf(target);
                var trimmed = target.TrimEnd('/', '\\');
                return Path.GetFileNameWithoutExtension(trimmed) is { Length: > 0 } n ? n : Path.GetFileName(trimmed) is { Length: > 0 } f ? f : trimmed;
        }
    }

    private static string DisplayNameOf(string directory)
    {
        var trimmed = directory.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Length > 0 ? name : trimmed; // ドライブ "C:\" → "C:"
    }
}
