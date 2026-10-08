using System.Text.Json;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;
using static FileLauncher.Core.Items.FolderOpening;

namespace FileLauncher.Tests;

public class FolderOpeningTests
{
    [Theory]
    [InlineData(FolderOpenTarget.ExistingTab)]
    [InlineData(FolderOpenTarget.NewWindow)]
    [InlineData(FolderOpenTarget.System)]
    public void 新しいウィンドウの指示があれば設定に関係なくNewWindow_無ければ設定値そのまま(FolderOpenTarget setting)
    {
        Assert.Equal(FolderOpenTarget.NewWindow, Resolve(setting, newWindow: true));
        Assert.Equal(setting, Resolve(setting, newWindow: false));
    }

    private static FileManagerWindow W(int h, bool visible = true, bool cloaked = false, bool minimized = false) => new(h, visible, cloaked, minimized);

    [Fact]
    public void 手前から見て可視かつcloakされていない最初のウィンドウを選び_最小化中も選ぶ()
    {
        Assert.Equal(2, PickWindow([W(1, cloaked: true), W(2), W(3)])!.Handle);
        Assert.Equal(2, PickWindow([W(1, visible: false), W(2), W(3)])!.Handle);
        Assert.Equal(1, PickWindow([W(1, minimized: true), W(2)])!.Handle);
        Assert.Null(PickWindow([W(1, cloaked: true), W(2, cloaked: true)]));
        Assert.Null(PickWindow([]));
    }

    [Fact]
    public void フォルダを開く先はキーが無ければ既存のタブ_文字列のcamelCaseで読み書きする()
    {
        Assert.Equal(FolderOpenTarget.ExistingTab, JsonSerializer.Deserialize<AppSettings>("{\"general\": {}}", JsonDefaults.Options)!.General.FolderOpenTarget);
        Assert.Equal(FolderOpenTarget.System, JsonSerializer.Deserialize<AppSettings>("{\"general\": {\"folderOpenTarget\": \"system\"}}", JsonDefaults.Options)!.General.FolderOpenTarget);

        var s = new AppSettings();
        s.General.FolderOpenTarget = FolderOpenTarget.NewWindow;
        Assert.Contains("\"folderOpenTarget\": \"newWindow\"", JsonSerializer.Serialize(s, JsonDefaults.Options));
    }

    [Fact]
    public void macOSではSystemをNewWindowとして扱い_他はそのまま()
    {
        Assert.Equal(FolderOpenTarget.NewWindow, ForMacOS(FolderOpenTarget.System));
        Assert.Equal(FolderOpenTarget.NewWindow, ForMacOS(FolderOpenTarget.NewWindow));
        Assert.Equal(FolderOpenTarget.ExistingTab, ForMacOS(FolderOpenTarget.ExistingTab));
    }

    [Fact]
    public void 格納フォルダは末尾の区切りを無視して求め_ルートや親の無いパスはnull()
    {
        Assert.Equal("/Users/a", ParentOf("/Users/a/b.txt"));
        Assert.Equal("/Users/a", ParentOf("/Users/a/dir/"));
        Assert.Equal("/", ParentOf("/Users"));
        Assert.Null(ParentOf("/"));
        Assert.Null(ParentOf("name"));
    }
}
