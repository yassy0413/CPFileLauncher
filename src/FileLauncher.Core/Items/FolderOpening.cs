using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>フォルダを開く先の判定（SPEC §5.2「フォルダを開く先」）。OS 非依存の純粋関数。</summary>
public static class FolderOpening
{
    /// <summary>Ctrl+クリック / Ctrl+Enter / メニュー「新しいウィンドウで開く」は設定に関係なく新しいウィンドウ。</summary>
    public static FolderOpenTarget Resolve(FolderOpenTarget setting, bool newWindow)
        => newWindow ? FolderOpenTarget.NewWindow : setting;

    /// <summary>macOS では System = NewWindow（どちらも open。設定画面は 2 択。SPEC §5.2「macOS: Finder のタブ」）。</summary>
    public static FolderOpenTarget ForMacOS(FolderOpenTarget target)
        => target == FolderOpenTarget.System ? FolderOpenTarget.NewWindow : target;

    /// <summary>格納フォルダ（末尾の区切りは無視）。ルートなど親が無ければ null。</summary>
    public static string? ParentOf(string path)
        => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } parent ? parent : null;

    /// <summary>タブを足す候補の Explorer ウィンドウ。</summary>
    public sealed record FileManagerWindow(nint Handle, bool IsVisible, bool IsCloaked, bool IsMinimized);

    /// <summary>Z オーダー順（手前から）の中で、可視かつ cloak されていない（別の仮想デスクトップでない）最初のもの。最小化中も選ぶ。</summary>
    public static FileManagerWindow? PickWindow(IReadOnlyList<FileManagerWindow> zOrderTopFirst)
        => zOrderTopFirst.FirstOrDefault(w => w.IsVisible && !w.IsCloaked);
}
