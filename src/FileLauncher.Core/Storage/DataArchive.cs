using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FileLauncher.Core.Storage;

/// <summary>インポートできない理由（文言にはしない。App が言語に合わせて文にする）。</summary>
public enum ArchiveError
{
    /// <summary>zip として読めない。</summary>
    NotZip,
    /// <summary>settings.json も board.json も入っていない。</summary>
    NoKnownEntries,
    /// <summary>エントリが JSON のオブジェクトでない（Entry = 名前）。</summary>
    EntryNotObject,
    /// <summary>エントリの JSON が壊れている（Entry = 名前、Detail = パーサのメッセージ）。</summary>
    EntryCorrupt,
}

/// <summary>インポートできない zip。Message はログ用の英語。</summary>
public sealed class ArchiveException(ArchiveError kind, string? entry = null, string? detail = null)
    : Exception($"{kind}{(entry is null ? "" : $" {entry}")}{(detail is null ? "" : $": {detail}")}")
{
    public ArchiveError Kind { get; } = kind;
    public string? Entry { get; } = entry;
    public string? Detail { get; } = detail;
}

/// <summary>zip から読み出した settings.json / board.json の中身（無いほうは null）と、background/ 配下のファイル（キーは "background/&lt;名前&gt;"）。</summary>
public sealed record ArchiveContents(string? Settings, string? Board, IReadOnlyDictionary<string, byte[]>? Files = null);

/// <summary>
/// 設定と盤面のエクスポート / インポート（spec/SETTINGS.md データタブ）。
/// zip には settings.json と board.json、背景画像が background/ のコピーならその画像を入れる（icons/ はキャッシュなので入れない）。
/// </summary>
public static class DataArchive
{
    public const string SettingsEntry = "settings.json";
    public const string BoardEntry = "board.json";

    public static string DefaultFileName(DateTime localNow) => $"CPFileLauncher-backup-{localNow:yyyyMMdd-HHmmss}.zip";

    /// <summary>今ディスクにある 2 ファイルを zip に書く（呼ぶ前に保存待ちを書き出しておくこと）。</summary>
    public static void Export(DataPaths paths, Stream destination)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (entry, file) in new[] { (SettingsEntry, paths.SettingsFile), (BoardEntry, paths.BoardFile) })
        {
            if (File.Exists(file)) zip.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
        }
        if (ManagedBackground(paths) is { } value && BackgroundImageStore.Resolve(paths, value) is { } image && File.Exists(image))
            zip.CreateEntryFromFile(image, BackgroundImageStore.Folder + "/" + Path.GetFileName(image), CompressionLevel.NoCompression); // 画像は圧縮済み
    }

    /// <summary>settings.json の appearance.background.image が background/ のコピーならその値。</summary>
    private static string? ManagedBackground(DataPaths paths)
    {
        try
        {
            if (!File.Exists(paths.SettingsFile)) return null;
            var node = JsonNode.Parse(File.ReadAllText(paths.SettingsFile), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var value = node?["appearance"]?["background"]?["image"]?.GetValue<string>();
            return BackgroundImageStore.IsManaged(value) ? value : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>zip を読み、中身が JSON のオブジェクトであることだけ確かめる（版の扱いは読み込み時の通常の規則に任せる）。</summary>
    /// <exception cref="ArchiveException">FileLauncher のエクスポートでない・壊れている。</exception>
    public static ArchiveContents Read(Stream source)
    {
        ZipArchive zip;
        try { zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException) { throw new ArchiveException(ArchiveError.NotZip); }
        using (zip)
        {
            string? settings = ReadEntry(zip, SettingsEntry);
            string? board = ReadEntry(zip, BoardEntry);
            if (settings is null && board is null)
                throw new ArchiveException(ArchiveError.NoKnownEntries);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries)
            {
                // background/ 直下のファイルだけ（".." や深い階層は捨てる）
                var parts = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                int at = Array.IndexOf(parts, BackgroundImageStore.Folder);
                if (at < 0 || at != parts.Length - 2 || parts.Contains("..") || entry.Length == 0) continue;
                using var ms = new MemoryStream();
                using (var es = entry.Open()) es.CopyTo(ms);
                files[BackgroundImageStore.Folder + "/" + parts[^1]] = ms.ToArray();
            }
            return new ArchiveContents(settings, board, files);
        }
    }

    private static string? ReadEntry(ZipArchive zip, string name)
    {
        // フォルダごと圧縮された zip でも見つけられるよう、ファイル名で探す
        var entry = zip.GetEntry(name) ?? zip.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        string text = reader.ReadToEnd();
        try
        {
            if (JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is not JsonObject)
                throw new ArchiveException(ArchiveError.EntryNotObject, name);
        }
        catch (JsonException ex)
        {
            throw new ArchiveException(ArchiveError.EntryCorrupt, name, ex.Message);
        }
        return text;
    }
}
