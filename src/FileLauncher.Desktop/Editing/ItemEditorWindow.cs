using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.App;

/// <summary>
/// アイテムの登録・編集ダイアログ（SPEC §6.4）。新規と編集で同じ画面。作業用の写しを編集し、OK でその写しを返す
/// （呼び出し側が元へ写す）。キャンセルは null。
/// </summary>
internal sealed class ItemEditorWindow : ChromeWindow
{
    private const double LabelWidth = 140;

    private static (string Text, ItemKind Kind)[] Kinds =>
        EnumNames.Options(ItemKind.File, ItemKind.Folder, ItemKind.App, ItemKind.Url, ItemKind.Command).Select(o => (o.Text, o.Value)).ToArray();

    private static readonly (ItemColor? Id, string Name)[] Colors =
        [(null, EnumNames.Color(null)), .. Enum.GetValues<ItemColor>().Select(c => ((ItemColor?)c, EnumNames.Color(c)))];

    private readonly LauncherItem _work;
    private readonly IconService? _icons;
    private readonly AppearanceSettings _appearance;
    private readonly Image _preview = new() { Width = 48, Height = 48, Stretch = Stretch.Uniform };

    // テストから操作するためのコントロール
    internal ComboBox KindBox { get; }
    internal TextBox TargetBox { get; }
    internal TextBox NameBox { get; }
    internal TextBox ArgsBox { get; }

    /// <summary>引数の行が出ているか（テスト用）。</summary>
    internal bool ArgsRowVisible => ArgsBox.Parent?.Parent is Control row && row.IsVisible;
    internal TextBox WorkingDirBox { get; }
    internal TextBox HotkeyBox { get; }
    internal Button OkButton { get; }
    internal TextBlock HotkeyNote { get; }
    internal List<Border> ColorSwatches { get; } = new();

    private ItemEditorWindow(LauncherItem original, bool isNew, IconService? icons, Page page, AppearanceSettings appearance)
    {
        _work = BoardEditing.Clone(original, keepHotkey: true);
        _icons = icons;
        _appearance = appearance;

        Title = AppInfo.TitlePrefix + (isNew ? Strings.ItemEditor_Title_New : Strings.ItemEditor_Title_Edit);
        TagCode = "ITEM";
        BodyWidth = 480;
        CancelResult = () => null; // × / Esc = キャンセル
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        KindBox = new ComboBox { ItemsSource = Kinds.Select(k => k.Text).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(Kinds, k => k.Kind == _work.Kind)), MinWidth = 200 };
        TargetBox = new TextBox { Text = _work.Target, MinWidth = 220 };
        NameBox = new TextBox { Text = _work.Name, MinWidth = 220, Watermark = Strings.ItemEditor_Name_Placeholder };
        ArgsBox = new TextBox { Text = _work.Args, MinWidth = 280 };
        WorkingDirBox = new TextBox { Text = _work.WorkingDir ?? "", MinWidth = 220, Watermark = Strings.ItemEditor_WorkingDir_Placeholder };
        HotkeyBox = new TextBox { Text = _work.Hotkey ?? "", MaxLength = 1, Width = 48 };
        HotkeyNote = Themed.Note(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });
        OkButton = Dialogs.ActionButton(Strings.Common_OK, isDefault: true);
        var cancel = Dialogs.ActionButton(Strings.Common_Cancel, isCancel: true);

        var browse = new Button { Content = Strings.Common_Browse };
        var browseDir = new Button { Content = Strings.Common_Browse };
        var argsNote = Themed.Note(new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Text = Strings.ItemEditor_Args_Note,
        });
        var launchMode = new ComboBox { ItemsSource = new[] { EnumNames.Of(LaunchMode.Normal), EnumNames.Of(LaunchMode.Minimized), EnumNames.Of(LaunchMode.Maximized) }, SelectedIndex = (int)_work.LaunchMode, MinWidth = 140 };
        var admin = new CheckBox { Content = Strings.ItemEditor_RunAsAdmin, IsChecked = _work.RunAsAdmin };

        var changeIcon = new Button { Content = Strings.ItemEditor_ChangeIcon };
        var resetIcon = new Button { Content = Strings.Common_ResetToDefault };

        ItemKind Kind() => Kinds[Math.Max(0, KindBox.SelectedIndex)].Kind;

        Control? argsRow = null, workingDirRow = null;
        void UpdateKindDependent()
        {
            var kind = Kind();
            browse.IsVisible = kind is ItemKind.File or ItemKind.Folder or ItemKind.App;
            // その OS・種別で使えない項目は出さない（無効表示 + 注記にしない。2026-10-03 ユーザー判断）:
            // macOS のファイルには引数を渡せない、URL には引数・作業フォルダが無い
            bool macFile = OperatingSystem.IsMacOS() && kind == ItemKind.File;
            if (argsRow is not null) argsRow.IsVisible = kind != ItemKind.Url && kind != ItemKind.Folder && !macFile;
            if (workingDirRow is not null) workingDirRow.IsVisible = kind is not (ItemKind.Url or ItemKind.Folder);
        }

        void Validate() => OkButton.IsEnabled = !string.IsNullOrWhiteSpace(TargetBox.Text);

        void UpdateHotkeyNote()
        {
            var key = ItemHotkey.Normalize(HotkeyBox.Text);
            var other = key is null ? null : ItemHotkey.Find(page, key, except: original);
            HotkeyNote.Text = !string.IsNullOrEmpty(HotkeyBox.Text) && key is null
                ? Strings.ItemEditor_Hotkey_Invalid
                : other is not null ? Strings.FormatItemEditor_Hotkey_InUse(other.Name) : Strings.ItemEditor_Hotkey_Note;
        }

        KindBox.SelectionChanged += (_, _) => { UpdateKindDependent(); RefreshPreview(); };
        TargetBox.TextChanged += (_, _) => { Validate(); RefreshPreview(); };
        HotkeyBox.TextChanged += (_, _) => UpdateHotkeyNote();
        HotkeyBox.LostFocus += (_, _) => HotkeyBox.Text = ItemHotkey.Normalize(HotkeyBox.Text) ?? "";

        browse.Click += async (_, _) =>
        {
            var path = await PickAsync(Kind() == ItemKind.Folder);
            if (path is null) return;
            TargetBox.Text = path;
            if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = ItemFactory.DefaultName(Kind(), path);
        };
        browseDir.Click += async (_, _) => { if (await PickAsync(folder: true) is { } d) WorkingDirBox.Text = d; };
        changeIcon.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.ItemEditor_IconPicker_Title,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(Strings.Common_ImageFiles) { Patterns = ["*.png", "*.ico", "*.icns", "*.svg"] }],
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } p)
            {
                _work.IconOverride = p;
                RefreshPreview();
            }
        };
        resetIcon.Click += (_, _) => { _work.IconOverride = null; RefreshPreview(); };

        // 色の見本（なし + 8 色）。選んでいるものは主色の太い枠
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void UpdateSwatches()
        {
            for (int i = 0; i < Colors.Length; i++)
            {
                var sw = ColorSwatches[i];
                bool selected = Colors[i].Id == _work.Color;
                sw.BorderThickness = new Thickness(selected ? 2 : 1);
                sw[!Border.BorderBrushProperty] = new DynamicResourceExtension(selected ? "FlAccent" : "FlEmptySlotBorder");
            }
        }
        foreach (var (id, name) in Colors)
        {
            var sw = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(3), Cursor = new Cursor(StandardCursorType.Hand) };
            if (id is { } c) sw[!Border.BackgroundProperty] = new DynamicResourceExtension(AppTheme.ItemColorKey(c));
            ToolTip.SetTip(sw, name);
            sw.PointerPressed += (_, _) => { _work.Color = id; UpdateSwatches(); };
            ColorSwatches.Add(sw);
            swatches.Children.Add(sw);
        }

        var form = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        form.Children.Add(Row(Strings.ItemEditor_Kind, KindBox));
        form.Children.Add(Row(Strings.ItemEditor_Target, Horizontal(TargetBox, browse)));
        form.Children.Add(Row(Strings.ItemEditor_Name, NameBox));
        form.Children.Add(argsRow = Row(Strings.ItemEditor_Args, ArgsBox, argsNote));
        form.Children.Add(workingDirRow = Row(Strings.ItemEditor_WorkingDir, Horizontal(WorkingDirBox, browseDir)));
        form.Children.Add(Row(Strings.ItemEditor_Icon, Horizontal(new Border { Width = 52, Height = 52, Child = _preview }, changeIcon, resetIcon)));
        if (OperatingSystem.IsWindows())
        {
            form.Children.Add(Row(Strings.ItemEditor_LaunchMode, launchMode));
            form.Children.Add(Row("", admin));
        }
        form.Children.Add(Row(Strings.ItemEditor_Hotkey, HotkeyBox, HotkeyNote));
        form.Children.Add(Row(Strings.ItemEditor_Color, swatches));
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 6, 0, 0), Children = { OkButton, cancel } });
        Body = form;

        OkButton.Click += (_, _) =>
        {
            var kind = Kind();
            _work.Kind = kind;
            _work.Target = TargetBox.Text!.Trim();
            _work.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? ItemFactory.DefaultName(kind, _work.Target) : NameBox.Text.Trim();
            _work.Args = ArgsBox.Text ?? "";
            _work.WorkingDir = string.IsNullOrWhiteSpace(WorkingDirBox.Text) ? null : WorkingDirBox.Text.Trim();
            _work.Hotkey = ItemHotkey.Normalize(HotkeyBox.Text);
            _work.LaunchMode = (LaunchMode)Math.Max(0, launchMode.SelectedIndex);
            _work.RunAsAdmin = admin.IsChecked == true;
            Close(_work);
        };
        cancel.Click += (_, _) => Close(null);

        UpdateKindDependent();
        Validate();
        UpdateHotkeyNote();
        UpdateSwatches();
        Opened += (_, _) => RefreshPreview();
    }

    /// <summary>今の入力（種別・パス・アイコンの上書き）でプレビューを引き直す。</summary>
    private void RefreshPreview()
    {
        if (_icons is null) return;
        var probe = BoardEditing.Clone(_work, keepHotkey: false);
        probe.Kind = Kinds[Math.Max(0, KindBox.SelectedIndex)].Kind;
        probe.Target = TargetBox.Text ?? "";
        int px = (int)Math.Round(48 * RenderScaling);
        _preview.Source = _icons.Get(probe, px, _appearance.ImageThumbnails, loaded => _preview.Source = loaded);
    }

    private async Task<string?> PickAsync(bool folder)
    {
        if (folder)
        {
            var dirs = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
            return dirs.FirstOrDefault()?.TryGetLocalPath();
        }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static Control Horizontal(params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var c in children)
        {
            c.VerticalAlignment = VerticalAlignment.Center;
            p.Children.Add(c);
        }
        return p;
    }

    private static Control Row(string label, Control control, TextBlock? note = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*") };
        var text = Themed.Glow(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) }, strong: false);
        control.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
        if (note is null) return grid;
        note.Margin = new Thickness(LabelWidth, 0, 0, 0);
        return new StackPanel { Spacing = 2, Children = { grid, note } };
    }

    public static Task<LauncherItem?> ShowAsync(Window owner, LauncherItem original, bool isNew, IconService? icons, Page page, AppearanceSettings appearance) =>
        new ItemEditorWindow(original, isNew, icons, page, appearance).ShowDialog<LauncherItem?>(owner);

    /// <summary>テスト用: ダイアログを作るだけ（表示はテストが行う）。</summary>
    internal static ItemEditorWindow Create(LauncherItem original, bool isNew, Page page, AppearanceSettings appearance) =>
        new(original, isNew, null, page, appearance);
}
