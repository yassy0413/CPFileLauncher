using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FileLauncher.Desktop.Tests;

/// <summary>
/// 製品名は CPFileLauncher（SPEC §1.3、2026-10-04）。旧名「FileLauncher」が UI の文言・配布物の設定に残っていないこと。
/// コードの名前空間・プロジェクト名（FileLauncher.*）は変えないので対象外。
/// </summary>
public sealed partial class ProductNameTests
{
    [GeneratedRegex(@"(?<![A-Za-z])FileLauncher(?![.\w])")]
    private static partial Regex OldName();

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileLauncher.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void 文言と配布物の設定に旧名が残っていない()
    {
        string root = Root();
        var hits = new List<string>();
        foreach (var resx in new[] { "Strings.resx", "Strings.ja.resx" })
        {
            foreach (var data in XDocument.Load(Path.Combine(root, "FileLauncher.Desktop", "Resources", resx)).Root!.Elements("data"))
            {
                string value = (string?)data.Element("value") ?? "";
                if (OldName().IsMatch(value)) hits.Add($"{resx} {(string)data.Attribute("name")!}: {value}");
            }
        }
        foreach (var rel in new[] { "FileLauncher.Desktop/macos/Info.plist", "FileLauncher.Desktop/app.manifest" })
        {
            var lines = File.ReadAllLines(Path.Combine(root, rel));
            for (int i = 0; i < lines.Length; i++)
                if (!lines[i].TrimStart().StartsWith("<!--", StringComparison.Ordinal) && OldName().IsMatch(lines[i])) hits.Add($"{rel}:{i + 1}: {lines[i].Trim()}");
        }
        // 画面側のコードの文字列リテラル（名前空間・旧フォルダ名の定数は除く）
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "FileLauncher.Desktop"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Regex.Matches(lines[i], "\"(?:[^\"\\\\]|\\\\.)*\""))
                    if (OldName().IsMatch(m.Value) && !lines[i].Contains("AppLog.") && !lines[i].Contains("i18n:ignore"))
                        hits.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(hits.Count == 0, string.Join("\n", hits));
    }
}
