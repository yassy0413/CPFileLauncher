using System.Text.Json;
using FileLauncher.Core.Model;

namespace FileLauncher.Core.Storage;

/// <summary>
/// 「元に戻す」（1 段階）用の盤面の写し（SPEC §6.7）。盤面全体を JSON で持つので、ページの削除も戻せる。
/// 戻すときは同じ <see cref="Board"/> インスタンスの Pages を差し替える（App / コントローラ / エディタが共有しているため）。
/// </summary>
public static class BoardSnapshot
{
    public static string Capture(Board board) => JsonSerializer.Serialize(board, JsonDefaults.Options);

    public static void Restore(Board board, string snapshot)
    {
        var restored = JsonSerializer.Deserialize<Board>(snapshot, JsonDefaults.Options)
            ?? throw new InvalidOperationException("board snapshot could not be read");
        board.Pages = restored.Pages;
        board.Normalize();
    }
}
