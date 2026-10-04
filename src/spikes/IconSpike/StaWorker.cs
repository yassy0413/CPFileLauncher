using System.Collections.Concurrent;

namespace IconSpike;

/// <summary>
/// シェルの COM API 用の専用 STA スレッド。UI スレッドを塞がずにアイコンを取得する。
/// 検証ポイント: メッセージポンプ無しの STA で、サムネイルハンドラ等が固まらないか。
/// </summary>
public sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaWorker(string name)
    {
        _thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable()) work();
        })
        { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> Run<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public void Dispose() => _queue.CompleteAdding();
}
