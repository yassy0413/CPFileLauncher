using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;

namespace FileLauncher.App;

/// <summary>ページの設定の結果。ButtonSize = null は共通設定を継承。</summary>
internal sealed record PageSettingsResult(string Name, int Rows, int Cols, int? ButtonSize);

/// <summary>
/// ページの設定ダイアログ（SPEC §6.5）: 名前 / 行数 / 列数 / ボタンサイズ。
/// 行列数を減らしてアイテムが入りきらないときは OK を押せない（消さない方針）。入りきるなら詰め直す件数を注記する。
/// </summary>
internal sealed class PageSettingsWindow : ChromeWindow
{
    private static readonly (string Text, int? Size)[] Sizes =
        [("", null), (Strings.Common_ButtonSize_Small, 32), (Strings.Common_ButtonSize_Medium, 48), (Strings.Common_ButtonSize_Large, 64), (Strings.Common_ButtonSize_ExtraLarge, 96)];

    private PageSettingsWindow(Page page, AppearanceSettings appearance)
    {
        Title = AppInfo.TitlePrefix + Strings.PageSettings_Title;
        TagCode = "PAGE";
        BodyWidth = 380;
        CancelResult = () => null; // × / Esc = キャンセル
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var name = new TextBox { Text = page.Name, MinWidth = 220 };
        var rows = new NumericUpDown { Minimum = Page.MinGrid, Maximum = Page.MaxGrid, Value = page.Rows, Increment = 1, FormatString = "0", MinWidth = 120 };
        var cols = new NumericUpDown { Minimum = Page.MinGrid, Maximum = Page.MaxGrid, Value = page.Cols, Increment = 1, FormatString = "0", MinWidth = 120 };
        var sizeItems = Sizes.Select(s => s.Size is null ? Strings.FormatPageSettings_ButtonSize_Inherit(appearance.ButtonSize) : s.Text).ToList();
        var size = new ComboBox { ItemsSource = sizeItems, SelectedIndex = Math.Max(0, Array.FindIndex(Sizes, s => s.Size == page.ButtonSize)), MinWidth = 220 };
        var note = Themed.Note(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });

        var ok = Dialogs.ActionButton(Strings.Common_OK, isDefault: true);
        var cancel = Dialogs.ActionButton(Strings.Common_Cancel, isCancel: true);

        void Validate()
        {
            int r = (int)(rows.Value ?? page.Rows), c = (int)(cols.Value ?? page.Cols);
            bool fits = BoardEditing.CanResize(page, r, c, out int overflow);
            ok.IsEnabled = fits && !string.IsNullOrWhiteSpace(name.Text);
            if (!fits)
            {
                int lacking = overflow - BoardEditing.CountEmptyWithin(page, r, c);
                note.Text = Loc.Count(lacking, Strings.PageSettings_DoesNotFit_One, Strings.PageSettings_DoesNotFit_Other);
                Themed.Foreground(note, "FlError");
            }
            else
            {
                note.Text = overflow > 0 ? Loc.Count(overflow, Strings.PageSettings_WillMove_One, Strings.PageSettings_WillMove_Other) : "";
                Themed.Note(note);
            }
        }
        rows.ValueChanged += (_, _) => Validate();
        cols.ValueChanged += (_, _) => Validate();
        name.TextChanged += (_, _) => Validate();
        Validate();

        Grid Row(string label, Control control)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*") };
            var t = Themed.Glow(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, strong: false);
            Grid.SetColumn(control, 1);
            control.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(t);
            g.Children.Add(control);
            return g;
        }

        Body = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                Row(Strings.PageSettings_Name, name),
                Row(Strings.PageSettings_Rows, rows),
                Row(Strings.PageSettings_Cols, cols),
                Row(Strings.PageSettings_ButtonSize, size),
                note,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { ok, cancel } },
            },
        };
        ok.Click += (_, _) => Close(new PageSettingsResult(name.Text!.Trim(), (int)rows.Value!, (int)cols.Value!, Sizes[Math.Max(0, size.SelectedIndex)].Size));
        cancel.Click += (_, _) => Close(null);
    }

    public static Task<PageSettingsResult?> ShowAsync(Window owner, Page page, AppearanceSettings appearance) =>
        new PageSettingsWindow(page, appearance).ShowDialog<PageSettingsResult?>(owner);
}
