using System.Security.Cryptography;
using System.Text;

namespace IconSpike;

/// <summary>
/// 抽出済みアイコンの png キャッシュ（SPEC §9.2 icons/）。
/// キー = パス（小文字化）+ ピクセルサイズ + 取得方式 + 対象の最終更新時刻。
/// 対象が更新されたら（exe の差し替え等）別キーになって再抽出される。
/// </summary>
public sealed class IconCache
{
    public string Directory { get; }

    public IconCache(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    public string PathFor(string source, int px, string variant)
    {
        long ticks = 0;
        try
        {
            if (File.Exists(source)) ticks = File.GetLastWriteTimeUtc(source).Ticks;
            else if (System.IO.Directory.Exists(source)) ticks = System.IO.Directory.GetLastWriteTimeUtc(source).Ticks;
        }
        catch { /* アクセス不可でもキャッシュキーは作る */ }

        string key = $"{source.ToLowerInvariant()}|{px}|{variant}|{ticks}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(key));
        return System.IO.Path.Combine(Directory, Convert.ToHexString(hash, 0, 10).ToLowerInvariant() + ".png");
    }

    public void Clear()
    {
        foreach (var f in System.IO.Directory.EnumerateFiles(Directory, "*.png")) File.Delete(f);
    }

    public (int Count, long Bytes) Stats()
    {
        var files = new DirectoryInfo(Directory).GetFiles("*.png");
        return (files.Length, files.Sum(f => f.Length));
    }
}
