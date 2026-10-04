namespace FileLauncher.Tests;

/// <summary>テストごとの一時フォルダ。Dispose で削除。</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FileLauncherTests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

/// <summary>バックアップ名のタイムスタンプを制御するための時計。</summary>
internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan by) => Now += by;
}
