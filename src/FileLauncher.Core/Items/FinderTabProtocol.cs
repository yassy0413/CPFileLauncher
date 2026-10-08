namespace FileLauncher.Core.Items;

/// <summary>
/// macOS の Finder のタブを開く osascript との握手（SPEC §5.2「macOS: Finder のタブ」）。
/// スクリプトは標準エラーへ <c>READY n</c> / <c>NOWINDOW</c> / <c>TIMEOUT</c> / <c>SELECTFAILED</c> / <c>DONE</c> を 1 行ずつ書く。
/// </summary>
public static class FinderTabProtocol
{
    public enum Kind { Ready, NoWindow, Done, Timeout, SelectFailed, Other }

    /// <summary>Count は Ready のときの Finder ウィンドウ（タブ）の数。それ以外は 0。</summary>
    public sealed record Line(Kind Kind, int Count = 0);

    /// <summary>1 行を読む。前後の空白と、osascript が付けることのある <c>(* … *)</c> を外す。大文字小文字は区別する。</summary>
    public static Line Parse(string? line)
    {
        string s = (line ?? "").Trim();
        if (s.StartsWith("(*", StringComparison.Ordinal) && s.EndsWith("*)", StringComparison.Ordinal)) s = s[2..^2].Trim();
        return s switch
        {
            "NOWINDOW" => new(Kind.NoWindow),
            "DONE" => new(Kind.Done),
            "TIMEOUT" => new(Kind.Timeout),
            "SELECTFAILED" => new(Kind.SelectFailed),
            _ when s.StartsWith("READY ", StringComparison.Ordinal) && int.TryParse(s[6..], out int n) && n > 0 => new(Kind.Ready, n),
            _ => new(Kind.Other),
        };
    }
}
