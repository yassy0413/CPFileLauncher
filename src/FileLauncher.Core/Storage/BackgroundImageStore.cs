using FileLauncher.Core.Items;

namespace FileLauncher.Core.Storage;

/// <summary>
/// 盤面の背景画像のファイル（SPEC §3.7）。設定画面で選んだ画像はデータフォルダの background/ にコピーし、
/// 設定には相対パス "background/&lt;名前&gt;" を書く（ポータブル・エクスポートで持ち運べるように）。手編集の絶対パスもそのまま使える。
/// </summary>
public static class BackgroundImageStore
{
    public const string Folder = "background";
    private const string Prefix = Folder + "/";

    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];

    /// <summary>background/ のコピーを指している値か（設定に書く区切りは常に "/"）。</summary>
    public static bool IsManaged(string? value) =>
        value is not null && (value.StartsWith(Prefix, StringComparison.Ordinal) || value.StartsWith(Folder + "\\", StringComparison.Ordinal));

    /// <summary>設定の値 → 実際のファイルパス。null / 空なら null。</summary>
    public static string? Resolve(DataPaths paths, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (IsManaged(value))
            return Path.Combine(paths.BackgroundDirectory, Path.GetFileName(value.Replace('\\', '/')));
        return LaunchArgs.ExpandPath(value);
    }

    /// <summary>
    /// 画像を background/ にコピーして設定に書く値を返す。background/ は 1 枚だけ（前の画像は消す）。
    /// コピー元がすでに background/ の中ならそのまま。
    /// </summary>
    public static string Import(DataPaths paths, string sourcePath)
    {
        string name = Path.GetFileName(sourcePath);
        string dest = Path.Combine(paths.BackgroundDirectory, name);
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return Prefix + name;

        Directory.CreateDirectory(paths.BackgroundDirectory);
        // 先に一時名でコピーしてから古いものを消す（コピーに失敗したら今の画像を残す）
        string tmp = dest + ".tmp";
        File.Copy(sourcePath, tmp, overwrite: true);
        foreach (var old in Directory.GetFiles(paths.BackgroundDirectory))
        {
            if (old != tmp) File.Delete(old);
        }
        File.Move(tmp, dest);
        return Prefix + name;
    }

    /// <summary>「クリア」: コピーなら消す（絶対パス参照の元ファイルは消さない）。</summary>
    public static void Remove(DataPaths paths, string? value)
    {
        if (!IsManaged(value) || Resolve(paths, value) is not { } file) return;
        try { File.Delete(file); }
        catch (IOException) { /* 使用中なら残す。次に選んだときに消える */ }
        catch (UnauthorizedAccessException) { }
    }
}
