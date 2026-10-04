using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileLauncher.Core.Storage;

/// <summary>settings.json / board.json 共通の JSON 形式（人間可読・camelCase・enum は文字列）。</summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 日本語のパスやページ名を \uXXXX にせずそのまま書く（手編集しやすさ優先）
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
