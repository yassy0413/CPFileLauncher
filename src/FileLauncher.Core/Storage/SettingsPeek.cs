using System.Text.Json;

namespace FileLauncher.Core.Storage;

/// <summary>Avalonia を起動する前に settings.json から 1 キーだけ読む（描画方式など、起動時にしか変えられない設定）。</summary>
public static class SettingsPeek
{
    /// <summary>advanced.hardwareAcceleration。ファイルが無い・壊れている・キーが無いときは既定の true。</summary>
    public static bool HardwareAcceleration(string settingsFile)
    {
        try
        {
            using var stream = File.OpenRead(settingsFile);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return !(doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("advanced", out var advanced)
                && advanced.ValueKind == JsonValueKind.Object
                && advanced.TryGetProperty("hardwareAcceleration", out var value)
                && value.ValueKind == JsonValueKind.False);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return true;
        }
    }

    /// <summary>general.language（"system" / "ja" / "en"）。ファイルが無い・壊れている・キーが無い・知らない値なら "system"。</summary>
    public static string Language(string settingsFile)
    {
        try
        {
            using var stream = File.OpenRead(settingsFile);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("general", out var general)
                && general.ValueKind == JsonValueKind.Object
                && general.TryGetProperty("language", out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is "ja" or "en")
                return value.GetString()!;
            return "system";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return "system";
        }
    }
}
