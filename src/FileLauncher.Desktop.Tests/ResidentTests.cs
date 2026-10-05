using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FileLauncher.App;
using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Desktop.Tests;

public sealed class ResidentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherUiTests", Guid.NewGuid().ToString("N"));
    private readonly FakePlatform _platform = new();
    private readonly SettingsHub _hub;
    private readonly BoardWindow _board = new();
    private readonly Board _data = Board.CreateDefault();

    public ResidentTests()
    {
        Directory.CreateDirectory(_dir);
        var store = new AppDataStore(new DataPaths(_dir, IsPortable: false));
        _hub = new SettingsHub(store, store.LoadAll().Settings.Value);
        _board.ApplyEffects(_hub.Current.Appearance);
        _board.Render(_data, _hub.Current.Appearance, 0);
    }

    public void Dispose()
    {
        _board.AllowClose = true;
        _board.Close();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private ResidentController NewResident()
    {
        var r = new ResidentController(_board, _platform, _hub);
        r.Attach();
        return r;
    }

    [AvaloniaFact]
    public void 常駐を有効にすると保存した位置に盤面が出る()
    {
        _hub.Update(s => s.Resident.Bounds = new WindowBounds(120, 140, 400, 300), SettingsChange.None);
        var r = NewResident();

        r.Activate();
        Dispatcher.UIThread.RunJobs();

        Assert.True(r.IsShown);
        Assert.Equal(new PixelPoint(120, 140), _board.Position);
        Assert.False(_board.Topmost); // 重ね順は SetZOrder で
    }

    [AvaloniaFact]
    public void 起動しても盤面は閉じない()
    {
        var r = NewResident();
        r.Activate();
        var item = new LauncherItem { Name = "x", Target = "/tmp", Kind = ItemKind.Folder };

        r.Launch(item, null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(r.IsShown);
        Assert.Contains("/tmp", _platform.Launched);
    }

    [AvaloniaFact]
    public void フォルダは設定の開き先で開き_新しいウィンドウの指示があれば設定に関係なく新しいウィンドウ()
    {
        var r = NewResident();
        r.Activate();
        var item = new LauncherItem { Name = "x", Target = "/tmp", Kind = ItemKind.Folder };

        r.Launch(item, null);
        Assert.Equal(FolderOpenTarget.ExistingTab, _platform.LastFolderTarget); // 既定

        _hub.Update(s => s.General.FolderOpenTarget = FolderOpenTarget.System, SettingsChange.None);
        r.Launch(item, null);
        Assert.Equal(FolderOpenTarget.System, _platform.LastFolderTarget);

        r.Launch(item, null, newWindow: true);
        Assert.Equal(FolderOpenTarget.NewWindow, _platform.LastFolderTarget);
    }

    [AvaloniaFact]
    public void 位置をロックするとドラッグで動かせなくなる()
    {
        var r = NewResident();
        r.Activate();

        _hub.Update(s => s.Resident.LockPosition = true, SettingsChange.Resident);
        r.ApplySettings();

        Assert.False(_board.AllowMove);
    }

    [AvaloniaFact]
    public void 無効にすると盤面を隠し_最後に動かした位置を保存する()
    {
        var r = NewResident();
        r.Activate();
        _board.Position = new PixelPoint(300, 200);
        Dispatcher.UIThread.RunJobs();
        var t = r.DeactivateAsync();
        _board.Position = new PixelPoint(0, 0); // 止めたあとの位置の通知（終了時の片付け）は無視される
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!t.IsCompleted && DateTime.UtcNow < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Dispatcher.UIThread.RunJobs();
        Assert.False(_board.IsVisible);
        Assert.Equal((300, 200), (_hub.Current.Resident.Bounds!.X, _hub.Current.Resident.Bounds.Y));
    }
}
