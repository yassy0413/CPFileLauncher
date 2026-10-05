using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using FileLauncher.Core.Items;
using FileLauncher.Core.Model;
using FileLauncher.Platform.Windows.Native;
using static FileLauncher.Core.Items.FolderOpening;

namespace FileLauncher.Platform.Windows;

/// <summary>
/// フォルダを既存の Explorer の新しいタブ / 新しいウィンドウで開く（SPEC §5.2「フォルダを開く先」、§10.3）。
/// 処理は専用の STA スレッド（ExplorerSTA）で続け、呼び出し元（UI スレッド）は待たない。
/// 既存のタブに入れられなければ新しいウィンドウへフォールバックする（ログだけ、トーストなし）。
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ExplorerTabs
{
    // 2026-10-05 スパイク（Windows 11 25H2 build 26200）: タブの出現 0.4〜0.6 s、移動完了まで計 0.6〜0.8 s
    private static readonly TimeSpan TabAppearTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NavigateTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan NewWindowTimeout = TimeSpan.FromSeconds(3);
    private const int PollMs = 30;

    private readonly StaWorker _sta;

    public ExplorerTabs(StaWorker sta) => _sta = sta;

    /// <param name="folder">開くフォルダ。</param>
    /// <param name="selectPath">選択する項目（格納フォルダを開くとき）。null なら選択しない。</param>
    public void OpenFolder(string folder, string? selectPath, FolderOpenTarget target, LaunchMode mode)
    {
        _ = _sta.Run(() =>
        {
            try
            {
                if (target == FolderOpenTarget.ExistingTab && TryOpenInExistingTab(folder, selectPath)) return true;
                OpenNewWindow(folder, selectPath, mode);
            }
            catch (Exception ex)
            {
                Log($"フォルダを開けませんでした: {folder}: {ex.Message}");
            }
            return true;
        });
    }

    private static bool TryOpenInExistingTab(string folder, string? selectPath)
    {
        var sw = Stopwatch.StartNew();
        var candidates = ExplorerWindows.TopWindows()
            .Select(h => new FileManagerWindow(h, ExplorerWindows.IsVisible(h), ExplorerWindows.IsCloaked(h), ExplorerWindows.IsMinimized(h)))
            .ToList();
        var picked = PickWindow(candidates);
        if (picked is null) { Log("existing tab: Explorer のウィンドウが無い → 新しいウィンドウ"); return false; }
        nint window = picked.Handle;

        nint active = ExplorerWindows.ActiveTab(window);
        if (active == 0) { Log("existing tab: タブの無い Explorer → 新しいウィンドウ"); return false; }

        var before = ExplorerWindows.Tabs().Where(t => t.Window == window).Select(t => t.TabHandle).ToHashSet();
        int countBefore = ExplorerWindows.TabCount(window);
        if (!ExplorerWindows.RequestNewTab(active)) { Log("existing tab: 新しいタブの要求に失敗 → 新しいウィンドウ"); return false; }

        // 新しいタブを待つ: 子ウィンドウの数（安い）が増えてから IShellWindows で特定する
        ExplorerWindows.Tab? added = null;
        while (added is null && sw.Elapsed < TabAppearTimeout)
        {
            Thread.Sleep(PollMs);
            if (ExplorerWindows.TabCount(window) <= countBefore) continue;
            added = ExplorerWindows.Tabs().FirstOrDefault(t => t.Window == window && t.TabHandle != 0 && !before.Contains(t.TabHandle));
        }
        if (added is null) { Log($"existing tab: 新しいタブが {TabAppearTimeout.TotalSeconds} 秒以内に見つからない → 新しいウィンドウ"); return false; }

        ExplorerWindows.Navigate(added, folder);
        if (selectPath is not null) SelectAfterNavigation(added, folder, selectPath, sw);

        if (picked.IsMinimized) ExplorerWindows.Restore(window);
        WindowsWindowService.ForceForeground(window);
        Log($"folder → existing tab (hwnd=0x{window:X}, {sw.ElapsedMilliseconds} ms): {folder}");
        return true;
    }

    private static void OpenNewWindow(string folder, string? selectPath, LaunchMode mode)
    {
        var before = selectPath is null ? null : ExplorerWindows.Tabs().Select(t => t.TabHandle).ToHashSet();
        var style = mode switch
        {
            LaunchMode.Minimized => ProcessWindowStyle.Minimized,
            LaunchMode.Maximized => ProcessWindowStyle.Maximized,
            _ => ProcessWindowStyle.Normal,
        };
        try
        {
            // OS の「フォルダーを新しいタブで開く」が ON でも新しいウィンドウになる verb（2026-10-05 スパイクで確認）
            using var _ = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true, Verb = "opennewwindow", WindowStyle = style });
        }
        catch (Win32Exception ex)
        {
            Log($"opennewwindow に失敗（{ex.Message}）→ explorer.exe");
            if (selectPath is not null) { StartExplorer($"/select,\"{selectPath}\""); return; }
            StartExplorer($"/n,\"{folder}\"");
            return;
        }
        Log($"folder → new window: {folder}");
        if (selectPath is null) return;

        // 新しいウィンドウを IShellWindows の差分で見つけて選択する。見つからなければ explorer.exe /select に任せる
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < NewWindowTimeout)
        {
            Thread.Sleep(PollMs);
            var tab = ExplorerWindows.Tabs().FirstOrDefault(t => !before!.Contains(t.TabHandle) && SamePath(t.Path, folder));
            if (tab is null) continue;
            if (!ExplorerWindows.SelectItem(tab, Path.GetFileName(selectPath))) Log($"選択できませんでした: {selectPath}");
            return;
        }
        Log("新しいウィンドウが見つからない → explorer.exe /select");
        StartExplorer($"/select,\"{selectPath}\"");
    }

    /// <summary>移動の完了（表示中のフォルダが folder になる）を待ってから選択する。失敗はログだけ（フォルダは開けている）。</summary>
    private static void SelectAfterNavigation(ExplorerWindows.Tab tab, string folder, string selectPath, Stopwatch sw)
    {
        var deadline = sw.Elapsed + NavigateTimeout;
        while (sw.Elapsed < deadline)
        {
            Thread.Sleep(PollMs);
            if (!SamePath(tab.Path, folder)) continue;
            if (ExplorerWindows.SelectItem(tab, Path.GetFileName(selectPath))) return;
        }
        Log($"選択できませんでした: {selectPath}");
    }

    private static bool SamePath(string? a, string b) =>
        a is not null && string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    private static void StartExplorer(string args)
    {
        try { using var _ = Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true }); }
        catch (Exception ex) { Log($"explorer.exe {args} に失敗: {ex.Message}"); }
    }

    private static void Log(string message) => Trace.WriteLine("[explorer] " + message);
}
