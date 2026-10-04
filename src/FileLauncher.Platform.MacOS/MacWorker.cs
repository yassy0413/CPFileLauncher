using System.Collections.Concurrent;
using System.Runtime.Versioning;
using FileLauncher.Platform.MacOS.Native;

namespace FileLauncher.Platform.MacOS;

/// <summary>
/// AppKit / CoreGraphics を UI スレッドの外で順に呼ぶ専用スレッド（Win の StaWorker に相当）。
/// 1 件ごとに autorelease pool を張り、NSImage などの一時オブジェクトをその場で解放する。
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();

    public MacWorker(string name)
    {
        var thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                using var _ = AutoreleasePool.Push();
                work();
            }
        })
        { IsBackground = true, Name = name };
        thread.Start();
    }

    public Task<T> Run<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested) { tcs.SetCanceled(cancellationToken); return; }
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public void Dispose() => _queue.CompleteAdding();
}
