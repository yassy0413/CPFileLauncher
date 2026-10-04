using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileLauncher.App;
using FileLauncher.Core.Effects;

namespace FileLauncher.Desktop.Tests;

/// <summary>UI 文字列のリソース（英語 Strings.resx / 日本語 Strings.ja.resx）の整合（SPEC §7「言語と UI 文字列」）。</summary>
public sealed partial class StringsTests
{
    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Resources", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static readonly Dictionary<string, string> En = Load("Strings.resx");
    private static readonly Dictionary<string, string> Ja = Load("Strings.ja.resx");

    [GeneratedRegex(@"(?<!\{)\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    private static string[] Args(string value) =>
        Placeholder().Matches(value).Select(m => m.Groups[1].Value).Distinct().Order().ToArray();

    [Fact]
    public void 英語と日本語のキーが一致する()
    {
        var onlyEn = En.Keys.Except(Ja.Keys).ToList();
        var onlyJa = Ja.Keys.Except(En.Keys).ToList();
        Assert.True(onlyEn.Count == 0 && onlyJa.Count == 0,
            $"英語だけ: {string.Join(", ", onlyEn)} / 日本語だけ: {string.Join(", ", onlyJa)}");
    }

    [Fact]
    public void 書式の引数が英語と日本語で同じで_空の値が無い()
    {
        var bad = En.Keys.Intersect(Ja.Keys)
            .Where(k => !Args(En[k]).SequenceEqual(Args(Ja[k])))
            .Select(k => $"{k}: en {{{string.Join(",", Args(En[k]))}}} / ja {{{string.Join(",", Args(Ja[k]))}}}")
            .ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad));
        Assert.DoesNotContain(En.Concat(Ja), kv => string.IsNullOrWhiteSpace(kv.Value) && kv.Key != "Common_ListSeparator");
    }

    [Fact]
    public void すべての演出とenumの値に表示名がある()
    {
        var missing = EffectCatalog.All.Select(d => "Effect_" + d.Id)
            .Concat(EnumNames.Localized.SelectMany(t => Enum.GetNames(t).Select(n => $"Enum_{t.Name}_{n}")))
            .Where(k => !En.ContainsKey(k))
            .ToList();
        Assert.True(missing.Count == 0, string.Join(", ", missing));
    }

    [Fact]
    public void カルチャを日本語にすると日本語_戻すと英語になる()
    {
        var saved = Strings.Culture;
        try
        {
            Strings.Culture = new CultureInfo("ja");
            Assert.Equal("一般", Strings.Settings_Tab_General);
            Strings.Culture = new CultureInfo("en");
            Assert.Equal("General", Strings.Settings_Tab_General);
        }
        finally { Strings.Culture = saved; }
    }
}
