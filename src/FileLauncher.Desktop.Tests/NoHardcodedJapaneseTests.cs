using System.Text.RegularExpressions;

namespace FileLauncher.Desktop.Tests;

/// <summary>
/// UI 文字列の直書きを防ぐ（SPEC §7「言語と UI 文字列」）。FileLauncher.Desktop の .cs で日本語を含む文字列リテラルがあれば失敗する。
/// 例外: ログ（AppLog.）・Commit の理由・行末に「i18n:ignore」と書いた行。コメントは見ない。
/// </summary>
public sealed partial class NoHardcodedJapaneseTests
{
    [GeneratedRegex("\"(?:[^\"\\\\]|\\\\.)*[\\u3040-\\u30ff\\u4e00-\\u9fff](?:[^\"\\\\]|\\\\.)*\"")]
    private static partial Regex JapaneseLiteral();

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileLauncher.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "FileLauncher.Desktop");
    }

    [Fact]
    public void 画面側のコードに日本語の文字列を直書きしていない()
    {
        var hits = new List<string>();
        string root = SourceRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, file);
            if (rel.StartsWith("obj", StringComparison.Ordinal) || rel.StartsWith("bin", StringComparison.Ordinal)) continue;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains("AppLog.") || line.Contains("Commit(") || line.Contains("i18n:ignore")) continue;
                string code = line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? "" : StripComment(line);
                if (JapaneseLiteral().IsMatch(code)) hits.Add($"{rel}:{i + 1}: {line.Trim()}");
            }
        }
        Assert.True(hits.Count == 0, "リソース（Strings.resx / Strings.ja.resx）に移してください:\n" + string.Join("\n", hits));
    }

    /// <summary>文字列の外にある // 以降を落とす。</summary>
    private static string StripComment(string line)
    {
        bool inString = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
            if (!inString && c == '/' && line[i + 1] == '/') return line[..i];
        }
        return line;
    }
}
