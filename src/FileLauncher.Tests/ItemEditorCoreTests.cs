using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public class ItemEditorCoreTests
{
    [Theory]
    [InlineData(ItemKind.File, "/Users/a/見積書.xlsx", "見積書")]
    [InlineData(ItemKind.App, "/Applications/Safari.app/", "Safari")]
    [InlineData(ItemKind.Folder, "/Users/a/案件A/", "案件A")]
    [InlineData(ItemKind.Url, "https://example.com/path", "example.com")]
    [InlineData(ItemKind.Url, "mailto:me@example.com", "me@example.com")]
    [InlineData(ItemKind.Command, "git status --short", "git")]
    [InlineData(ItemKind.File, "  ", "")]
    public void 表示名の既定は種別ごとの規則で決まる(ItemKind kind, string target, string expected)
    {
        Assert.Equal(expected, ItemFactory.DefaultName(kind, target));
    }

    [Theory]
    [InlineData("a", "A")]
    [InlineData("7", "7")]
    [InlineData(" z ", "Z")]
    [InlineData("ab", null)]
    [InlineData("", null)]
    [InlineData("-", null)]
    [InlineData("あ", null)]
    public void ショートカットキーは英数字1字を大文字にする(string input, string? expected)
    {
        Assert.Equal(expected, ItemHotkey.Normalize(input));
    }

    [Fact]
    public void ショートカットキーの重複は同じページの自分以外から探す()
    {
        var page = new Page();
        var a = new LauncherItem { Name = "a", Hotkey = "A" };
        var b = new LauncherItem { Name = "b" };
        page.Items.Add(a);
        page.Items.Add(b);

        Assert.Same(a, ItemHotkey.Find(page, "a", except: b));
        Assert.Null(ItemHotkey.Find(page, "A", except: a));
    }
}
