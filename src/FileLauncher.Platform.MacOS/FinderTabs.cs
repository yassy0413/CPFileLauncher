using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using FileLauncher.Core.Items;
using FileLauncher.Platform.MacOS.Native;
using static FileLauncher.Core.Items.FinderTabProtocol;
using static FileLauncher.Platform.MacOS.Native.ObjC;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// フォルダを Finder の手前のウィンドウの新しいタブで開く（SPEC §5.2「macOS: Finder のタブ」、§10.3）。ExplorerTabs の macOS 版。
/// 新しいタブは AX でメニュー「新規タブ」を押して作り、フォルダの指定・選択・前面化は osascript（Apple Events）で行う。
/// 処理は専用ワーカー（FinderTabs）で続け、呼び出し元（UI スレッド）は待たない。
/// 許可が無い・Finder のウィンドウが無い・失敗したら従来の open（新しいウィンドウ）へフォールバックする（ログだけ、トーストなし）。
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class FinderTabs
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(6);

    /// <summary>
    /// macOS 10.14 以降はタブも Finder window として数えられる。押す前の数 n を READY で知らせ、
    /// 数が n を超える（= タブが出た）まで待ってから target を変える。待たないと元のタブが置き換わる。
    /// </summary>
    private const string Script = """
        on run argv
        	set folderPath to item 1 of argv
        	set selectPath to item 2 of argv
        	tell application "Finder" to set n to count of Finder windows
        	if n = 0 then
        		log "NOWINDOW"
        		return
        	end if
        	log "READY " & n
        	set opened to false
        	repeat 40 times
        		tell application "Finder" to set m to count of Finder windows
        		if m > n then
        			set opened to true
        			exit repeat
        		end if
        		delay 0.05
        	end repeat
        	if not opened then
        		log "TIMEOUT"
        		return
        	end if
        	tell application "Finder"
        		set target of Finder window 1 to (POSIX file folderPath as alias)
        		if selectPath is not "" then
        			try
        				select (POSIX file selectPath as alias)
        			on error
        				log "SELECTFAILED"
        			end try
        		end if
        		activate
        	end tell
        	log "DONE"
        end run
        """;

    private readonly MacWorker _worker;

    public FinderTabs(MacWorker worker) => _worker = worker;

    /// <param name="folder">開くフォルダ。</param>
    /// <param name="selectPath">選択する項目（格納フォルダを開くとき）。null なら選択しない。</param>
    public void OpenFolder(string folder, string? selectPath)
    {
        _ = _worker.Run(() =>
        {
            try
            {
                if (TryOpenInTab(folder, selectPath)) return true;
            }
            catch (Exception ex)
            {
                Log($"タブで開けませんでした: {folder}: {ex.Message}");
            }
            Fallback(folder, selectPath);
            return true;
        });
    }

    private static bool TryOpenInTab(string folder, string? selectPath)
    {
        // 未確認ならここで OS の許可ダイアログが出る（ユーザーがフォルダを開いたとき。SPEC §4.3）
        int permission = AppleEvents.DeterminePermission("com.apple.finder", ask: true);
        if (permission != AppleEvents.noErr)
        {
            Log(permission switch
            {
                AppleEvents.errAEEventNotPermitted => "automation denied (-1743) → 新しいウィンドウ",
                AppleEvents.procNotFound => "Finder が動いていない → 新しいウィンドウ",
                _ => $"権限の確認に失敗 ({permission}) → 新しいウィンドウ",
            });
            return false;
        }

        int pid = FinderPid();
        if (pid == 0) { Log("Finder の pid が取れない → 新しいウィンドウ"); return false; }

        var sw = Stopwatch.StartNew();
        var psi = new ProcessStartInfo("/usr/bin/osascript")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            ArgumentList = { "-e", Script, folder, selectPath ?? "" },
        };
        using var p = Process.Start(psi);
        if (p is null) { Log("osascript を起動できない → 新しいウィンドウ"); return false; }

        var lines = new BlockingCollection<string>();
        var errors = new List<string>();
        p.ErrorDataReceived += (_, e) => { if (e.Data is null) lines.CompleteAdding(); else lines.Add(e.Data); };
        p.OutputDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();

        bool ready = false, done = false;
        try
        {
            while (!lines.IsCompleted)
            {
                var limit = (ready ? TotalTimeout : HandshakeTimeout) - sw.Elapsed;
                if (limit <= TimeSpan.Zero || !lines.TryTake(out string? raw, limit))
                {
                    if (lines.IsCompleted) break;
                    Log(ready ? "osascript が時間内に終わらない → 新しいウィンドウ" : "osascript の応答が無い → 新しいウィンドウ");
                    Kill(p);
                    return false;
                }
                var line = Parse(raw);
                switch (line.Kind)
                {
                    case Kind.NoWindow:
                        Log("Finder のウィンドウが無い → 新しいウィンドウ");
                        return false;
                    case Kind.Ready:
                        ready = true;
                        if (!FinderMenu.PressNewTab(pid)) { Kill(p); Log("新規タブを押せない → 新しいウィンドウ"); return false; }
                        break;
                    case Kind.Timeout:
                        Log("新しいタブが 2 秒以内に出ない → 新しいウィンドウ");
                        return false;
                    case Kind.SelectFailed:
                        Log($"選択できませんでした: {selectPath}");
                        break;
                    case Kind.Done:
                        done = true;
                        break;
                    default:
                        errors.Add(raw);
                        break;
                }
            }
        }
        finally
        {
            if (!p.WaitForExit(TotalTimeout - sw.Elapsed > TimeSpan.Zero ? TotalTimeout - sw.Elapsed : TimeSpan.Zero)) Kill(p);
        }

        if (!done || p.ExitCode != 0)
        {
            Log($"osascript 失敗 (exit {(p.HasExited ? p.ExitCode : -1)}): {string.Join(" / ", errors)} → 新しいウィンドウ");
            // タブを足した後の失敗でも、作ったタブは閉じない（SPEC §5.2）
            return false;
        }
        Log($"folder → finder tab ({sw.ElapsedMilliseconds} ms): {folder}");
        return true;
    }

    private static void Fallback(string folder, string? selectPath)
    {
        var result = selectPath is null ? MacShellService.RunOpen([folder]) : MacShellService.RunOpen(["-R", selectPath]);
        Log(result.Success ? $"folder → new window: {folder}" : $"新しいウィンドウでも開けませんでした: {folder}: {result.Error}");
    }

    private static int FinderPid()
    {
        nint apps = Send(Class("NSRunningApplication"), Sel("runningApplicationsWithBundleIdentifier:"), NSString("com.apple.finder"));
        nint first = apps == 0 ? 0 : Send(apps, Sel("firstObject"));
        return first == 0 ? 0 : SendRetInt(first, Sel("processIdentifier"));
    }

    private static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(); } catch (InvalidOperationException) { }
    }

    private static void Log(string message) => Trace.WriteLine("[finder] " + message);
}
