using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.Tests;

public class TerminalLocationTests
{
    // 実行 OS のパスで書く（存在しなくてよい）
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "fl-terminal");
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;

    private static LauncherItem Item(ItemKind kind, string target, string? workingDir = null, string? linkPath = null)
        => new() { Kind = kind, Target = target, WorkingDir = workingDir, LinkPath = linkPath };

    [Fact]
    public void フォルダはそのフォルダ_末尾の区切りは落とし_ルートはそのまま()
    {
        string dir = Path.Combine(Base, "Folder");
        Assert.Equal(dir, TerminalLocation.For(Item(ItemKind.Folder, dir)));
        Assert.Equal(dir, TerminalLocation.For(Item(ItemKind.Folder, dir + Path.DirectorySeparatorChar)));
        Assert.Equal(Root, TerminalLocation.For(Item(ItemKind.Folder, Root)));
    }

    [Theory]
    [InlineData(ItemKind.File)]
    [InlineData(ItemKind.App)]
    public void ファイルとアプリは作業フォルダを優先し_無ければ実体の親(ItemKind kind)
    {
        string target = Path.Combine(Base, "sub", "tool.exe");
        string wd = Path.Combine(Base, "work");
        Assert.Equal(Path.Combine(Base, "sub"), TerminalLocation.For(Item(kind, target)));
        Assert.Equal(wd, TerminalLocation.For(Item(kind, target, workingDir: wd)));
        Assert.Equal(Path.Combine(Base, "sub"), TerminalLocation.For(Item(kind, target, workingDir: " ")));
    }

    [Fact]
    public void ショートカットから登録したアイテムはLinkPathではなく実体側()
    {
        string target = Path.Combine(Base, "real", "app.exe");
        string link = Path.Combine(Base, "links", "app.lnk");
        Assert.Equal(Path.Combine(Base, "real"), TerminalLocation.For(Item(ItemKind.App, target, linkPath: link)));
    }

    [Fact]
    public void コマンドは作業フォルダがあるときだけ_URLは常に出さない()
    {
        string wd = Path.Combine(Base, "work");
        Assert.Equal(wd, TerminalLocation.For(Item(ItemKind.Command, "echo hi", workingDir: wd)));
        Assert.Null(TerminalLocation.For(Item(ItemKind.Command, "echo hi")));
        Assert.Null(TerminalLocation.For(Item(ItemKind.Url, "https://example.com", workingDir: wd)));
    }

    [Fact]
    public void 親が取れないものや空のパスは出さない()
    {
        Assert.Null(TerminalLocation.For(Item(ItemKind.File, Root)));
        Assert.Null(TerminalLocation.For(Item(ItemKind.File, "tool.exe")));
        Assert.Null(TerminalLocation.For(Item(ItemKind.File, "")));
        Assert.Null(TerminalLocation.For(Item(ItemKind.Folder, "")));
    }
}
