using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace FileLauncher.App;

/// <summary>
/// 単一インスタンス制御（SPEC §10.3）。名前付き Mutex で 2 重起動を検知し、2 つ目は名前付きパイプで
/// 既存インスタンスに「盤面を表示」を送って終了する。データフォルダごとに別インスタンス扱い（ポータブル版と共存できる）。
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly string _name;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private bool _owned;

    public SingleInstance(string dataRoot)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataRoot.ToLowerInvariant()));
        _name = $"{AppInfo.ProductName}-{Environment.UserName}-{Convert.ToHexString(hash, 0, 6)}";
        // Global\ を付ける: 付けないと Unix（macOS）ではログインセッションごとの名前になり、Finder（LaunchServices）から起動したものと
        // ターミナルから起動したものが互いを見つけられない（2026-10-04 に .app で発生）。名前にユーザー名を含むので他のユーザーとはぶつからない。
        // パイプは Global\ を付けない（Unix ではユーザーごとの一時フォルダのソケットなので、どの起動元からも同じ）
        _mutex = new Mutex(false, @"Global\" + _name);
    }

    public bool TryAcquire()
    {
        try { _owned = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _owned = true; } // 前回異常終了
        return _owned;
    }

    /// <summary>既存インスタンスに「盤面を表示」を送る。</summary>
    public bool SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out);
            client.Connect(1000);
            client.WriteByte(1);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>2 つ目のインスタンスからの通知を待ち受ける（バックグラウンド）。onSignal はスレッドプールから呼ばれる。</summary>
    public void Listen(Action onSignal)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    if (server.ReadByte() == 1) onSignal();
                }
                catch (OperationCanceledException) { return; }
                catch (IOException) { await Task.Delay(200); }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_owned) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
