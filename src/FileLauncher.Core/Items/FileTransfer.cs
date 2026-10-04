using System.Diagnostics;
using System.Security.Cryptography;

namespace FileLauncher.Core.Items;

/// <summary>Added = そのままの名前で入れた件数、Renamed = 「名前 2」等の別名で入れた件数。</summary>
public sealed record TransferResult(int Added, int Renamed, int Skipped, int Failed);

/// <summary>
/// Drop-to-Open でフォルダアイテムへコピー / 移動する（SPEC §5.3。Windows / macOS 共通、OS のダイアログは出さない）。
/// 候補名「名前.ext」「名前 2.ext」「名前 3.ext」… を順に見て、同一内容のものがあればスキップ（移動でも元は消さない）、
/// 別内容なら次の候補へ、空いていればそこへ入れる。
/// </summary>
public static class FileTransfer
{
    public static Task<TransferResult> TransferAsync(string folder, IReadOnlyList<string> sources, bool move, CancellationToken ct = default) =>
        Task.Run(() => Transfer(folder, sources, move, ct), ct);

    public static TransferResult Transfer(string folder, IReadOnlyList<string> sources, bool move, CancellationToken ct = default)
    {
        int added = 0, renamed = 0, skipped = 0, failed = 0;
        foreach (var raw in sources)
        {
            ct.ThrowIfCancellationRequested();
            string src = raw.TrimEnd('/', '\\');
            try
            {
                bool first = true;
                foreach (var dest in CandidateNames(folder, Path.GetFileName(src), Directory.Exists(src)))
                {
                    if (SamePath(src, dest) || (Exists(dest) && AreIdentical(src, dest)))
                    {
                        skipped++;
                        break;
                    }
                    if (!Exists(dest))
                    {
                        Move(src, dest, move);
                        if (first) added++; else renamed++;
                        break;
                    }
                    first = false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
                Trace.WriteLine($"FileTransfer {src} → {folder} failed: {ex.Message}");
            }
        }
        return new TransferResult(added, renamed, skipped, failed);
    }

    /// <summary>"報告書.pdf" → 報告書.pdf, 報告書 2.pdf, 報告書 3.pdf, …（Finder の「両方を残す」と同じ形）。フォルダは拡張子なし扱い。</summary>
    public static IEnumerable<string> CandidateNames(string folder, string name, bool isDirectory)
    {
        string ext = isDirectory ? "" : Path.GetExtension(name);
        string stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        if (stem.Length == 0) { stem = name; ext = ""; } // ".gitignore" のような名前
        yield return Path.Combine(folder, name);
        for (int n = 2; ; n++) yield return Path.Combine(folder, $"{stem} {n}{ext}");
    }

    /// <summary>ファイルはサイズ + SHA-256、フォルダは相対パスの集合と各ファイル、シンボリックリンクはリンク先文字列で比較。</summary>
    public static bool AreIdentical(string a, string b)
    {
        string? linkA = LinkTarget(a), linkB = LinkTarget(b);
        if (linkA is not null || linkB is not null) return linkA == linkB;
        if (File.Exists(a) && File.Exists(b)) return SameFile(a, b);
        if (!Directory.Exists(a) || !Directory.Exists(b)) return false;

        var filesA = RelativeFiles(a);
        var filesB = RelativeFiles(b);
        return filesA.SetEquals(filesB) && filesA.All(rel => SameFile(Path.Combine(a, rel), Path.Combine(b, rel)));
    }

    private static void Move(string src, string dest, bool move)
    {
        if (!move)
        {
            Copy(src, dest);
            return;
        }
        try
        {
            if (Directory.Exists(src)) Directory.Move(src, dest);
            else File.Move(src, dest);
        }
        catch (IOException) when (!Exists(dest))
        {
            // 別ボリューム: コピーしてから元を消す（コピーに失敗したら元は残る）
            Copy(src, dest);
            if (Directory.Exists(src)) Directory.Delete(src, recursive: true);
            else File.Delete(src);
        }
    }

    private static void Copy(string src, string dest)
    {
        if (!Directory.Exists(src))
        {
            File.Copy(src, dest);
            return;
        }
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(src)) File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(src)) Copy(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget;
        }
        catch (IOException) { return null; }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || LinkTarget(path) is not null;

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static HashSet<string> RelativeFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f))
            .ToHashSet(StringComparer.Ordinal);

    private static bool SameFile(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var sa = File.OpenRead(a);
        using var sb = File.OpenRead(b);
        return SHA256.HashData(sa).AsSpan().SequenceEqual(SHA256.HashData(sb));
    }
}
