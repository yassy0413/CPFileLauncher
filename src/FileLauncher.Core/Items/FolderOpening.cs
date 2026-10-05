using FileLauncher.Core.Model;

namespace FileLauncher.Core.Items;

/// <summary>フォルダを開く先の判定（SPEC §5.2「フォルダを開く先」）。OS 非依存の純粋関数。</summary>
public static class FolderOpening
{
    /// <summary>Ctrl+クリック / Ctrl+Enter / メニュー「新しいウィンドウで開く」は設定に関係なく新しいウィンドウ。</summary>
    public static FolderOpenTarget Resolve(FolderOpenTarget setting, bool newWindow)
        => newWindow ? FolderOpenTarget.NewWindow : setting;

    /// <summary>タブを足す候補の Explorer ウィンドウ。</summary>
    public sealed record FileManagerWindow(nint Handle, bool IsVisible, bool IsCloaked, bool IsMinimized);

    /// <summary>Z オーダー順（手前から）の中で、可視かつ cloak されていない（別の仮想デスクトップでない）最初のもの。最小化中も選ぶ。</summary>
    public static FileManagerWindow? PickWindow(IReadOnlyList<FileManagerWindow> zOrderTopFirst)
        => zOrderTopFirst.FirstOrDefault(w => w.IsVisible && !w.IsCloaked);
}
