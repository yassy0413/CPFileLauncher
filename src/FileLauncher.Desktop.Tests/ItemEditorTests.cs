using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FileLauncher.App;
using FileLauncher.Core.Model;

namespace FileLauncher.Desktop.Tests;

public sealed class ItemEditorTests
{
    private readonly Window _owner = new() { Width = 200, Height = 200 };
    private readonly Page _page = new();

    private (ItemEditorWindow Dialog, Task<LauncherItem?> Result) Open(LauncherItem original, bool isNew)
    {
        _owner.Show();
        var w = ItemEditorWindow.Create(original, isNew, _page, new AppearanceSettings());
        var task = w.ShowDialog<LauncherItem?>(_owner);
        Dispatcher.UIThread.RunJobs();
        return (w, task);
    }

    /// <summary>コントロールの中央を実際にマウスで押す。</summary>
    private static void Press(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void パスが空ならOKを押せず_表示名が空なら既定の名前で登録される()
    {
        var (w, result) = Open(new LauncherItem(), isNew: true);
        Assert.False(w.OkButton.IsEnabled);

        w.TargetBox.Text = "/Users/me/見積書.xlsx";
        Dispatcher.UIThread.RunJobs();
        Assert.True(w.OkButton.IsEnabled);
        Click(w.OkButton);

        Assert.True(result.IsCompleted);
        Assert.Equal(("見積書", "/Users/me/見積書.xlsx"), (result.Result!.Name, result.Result.Target));
    }

    [AvaloniaFact]
    public void キャンセルするとnullを返し元のアイテムは変わらない()
    {
        var original = new LauncherItem { Name = "a", Target = "/a", Color = ItemColor.Red };
        var (w, result) = Open(original, isNew: false);
        w.NameBox.Text = "changed";
        Press(w, w.ColorSwatches[0]);
        w.Close(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(result.Result);
        Assert.Equal(("a", ItemColor.Red), (original.Name, original.Color));
    }

    [AvaloniaFact]
    public void 色の見本を押すとその色になる()
    {
        var (w, result) = Open(new LauncherItem { Name = "a", Target = "/a" }, isNew: false);
        var purple = w.ColorSwatches[7]; // なし, 赤, 橙, 黄, 緑, 水色, 青, 紫
        Press(w, purple);
        Click(w.OkButton);

        Assert.Equal(ItemColor.Purple, result.Result!.Color);
    }

    [AvaloniaFact]
    public void 同じページで使われているショートカットキーは注記が出る()
    {
        _page.Items.Add(new LauncherItem { Name = "Other", Hotkey = "K" });
        var (w, _) = Open(new LauncherItem { Name = "me", Target = "/me" }, isNew: false);

        w.HotkeyBox.Text = "k";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Other", w.HotkeyNote.Text);
        w.Close(null);
    }

    [AvaloniaFact]
    public void Macではファイルの引数欄を出さず_アプリにすると出る()
    {
        var (w, _) = Open(new LauncherItem { Kind = ItemKind.File, Target = "/a.txt" }, isNew: false);
        Assert.Equal(!OperatingSystem.IsMacOS(), w.ArgsRowVisible);
        w.KindBox.SelectedIndex = 2; // アプリケーション
        Dispatcher.UIThread.RunJobs();
        Assert.True(w.ArgsRowVisible);
        w.KindBox.SelectedIndex = 3; // URL には引数が無い
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.ArgsRowVisible);
        w.Close(null);
    }

    [AvaloniaFact]
    public void 編集結果を写してもIdと位置は変わらない()
    {
        var original = new LauncherItem { Row = 2, Col = 3, Name = "a" };
        var edited = new LauncherItem { Row = 0, Col = 0, Name = "b", Target = "/b", Color = ItemColor.Blue, Hotkey = "B" };

        BoardEditor.CopyEditable(edited, original);

        Assert.Equal(("b", "/b", ItemColor.Blue, "B", 2, 3), (original.Name, original.Target, original.Color, original.Hotkey, original.Row, original.Col));
        Assert.NotEqual(edited.Id, original.Id);
    }
}
