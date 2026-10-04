using System.Security.Cryptography;
using System.Text;
using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>
/// アイコンキャッシュ（icons/*.png）のファイル名（SPEC §5.4）。
/// キー = 取得元 + ピクセルサイズ + 取得方式 + 取得元の最終更新時刻。取得元が更新されたら別名になり再取得される。
/// </summary>
public static class IconCacheKey
{
    /// <summary>アイテムのアイコンの取得元。上書きアイコン &gt; .lnk 自身 &gt; 対象。URL は既定ブラウザなのでスキームで共有。</summary>
    public static string SourceOf(LauncherItem item)
    {
        if (!string.IsNullOrEmpty(item.IconOverride)) return LaunchArgs.ExpandPath(item.IconOverride);
        if (item.Kind == ItemKind.Url)
            return Uri.TryCreate(item.Target, UriKind.Absolute, out var u) ? $"url:{u.Scheme}" : "url:";
        if (item.Kind == ItemKind.Command) return "command:";
        return LaunchArgs.ExpandPath(item.LinkPath ?? item.Target);
    }

    /// <param name="variant">取得方式（"override" / "icon" / "thumb" など）。</param>
    public static string FileName(string source, int px, string variant)
    {
        long ticks = 0;
        try
        {
            // UNC などで時間がかかりうるので、ネットワークパスは更新時刻を見ない。
            // フォルダは中身が変わるたびに更新時刻が変わるが、アイコンは変わらないので見ない
            if (!source.StartsWith(@"\\", StringComparison.Ordinal) && File.Exists(source))
                ticks = File.GetLastWriteTimeUtc(source).Ticks;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        string key = $"{source.ToLowerInvariant()}|{px}|{variant}|{ticks}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)), 0, 10).ToLowerInvariant() + ".png";
    }
}
