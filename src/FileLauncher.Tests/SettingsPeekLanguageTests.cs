using FileLauncher.Core.Model;
using FileLauncher.Core.Storage;

namespace FileLauncher.Tests;

public sealed class SettingsPeekLanguageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FileLauncherTests", Guid.NewGuid().ToString("N"));

    public SettingsPeekLanguageTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void 言語の先読みは_jaとen以外はすべてsystemになる()
    {
        string file = Path.Combine(_dir, "settings.json");
        Assert.Equal("system", SettingsPeek.Language(file)); // ファイルなし
        File.WriteAllText(file, """{ "general": { "displayMode": "popup" } }""");
        Assert.Equal("system", SettingsPeek.Language(file));
        File.WriteAllText(file, """{ "general": { "language": "en" } }""");
        Assert.Equal("en", SettingsPeek.Language(file));
        File.WriteAllText(file, """{ "general": { "language": "fr" } }""");
        Assert.Equal("system", SettingsPeek.Language(file));
        File.WriteAllText(file, """{ "general": { "language": "ja" """);
        Assert.Equal("system", SettingsPeek.Language(file)); // 壊れた JSON
    }

    [Fact]
    public void 知らない言語の値は読み込み時にsystemへ丸める()
    {
        var s = new AppSettings();
        s.General.Language = "fr";
        s.Normalize();
        Assert.Equal("system", s.General.Language);
        s.General.Language = "ja";
        s.Normalize();
        Assert.Equal("ja", s.General.Language);
    }
}
