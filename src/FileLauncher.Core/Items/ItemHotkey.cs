using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>アイテムのショートカットキー（盤面表示中に押すと起動する英数字 1 字。SPEC §5.1 / §6.4）。</summary>
public static class ItemHotkey
{
    /// <summary>英数字 1 字なら大文字にして返す。それ以外（空・2 字以上・記号・日本語）は null。</summary>
    public static string? Normalize(string? text)
    {
        var t = text?.Trim();
        if (t is not { Length: 1 } || !char.IsAsciiLetterOrDigit(t[0])) return null;
        return t.ToUpperInvariant();
    }

    /// <summary>そのページでキーを持つアイテム（except 以外）。重複の注記に使う。</summary>
    public static LauncherItem? Find(Page page, string key, LauncherItem? except = null) =>
        page.Items.FirstOrDefault(i => !ReferenceEquals(i, except) && string.Equals(i.Hotkey, key, StringComparison.OrdinalIgnoreCase));
}
