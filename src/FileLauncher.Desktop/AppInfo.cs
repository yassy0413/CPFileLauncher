namespace FileLauncher.App;

/// <summary>製品名（SPEC §1.3 / §13.2 T1。2026-10-04 確定）。画面・タイトル・ツールチップの製品名はここから取る。</summary>
internal static class AppInfo
{
    public const string ProductName = "CPFileLauncher";
    public const string Subtitle = "CyberPunk FileLauncher"; // i18n:ignore 製品名の副題（旧名ではない）
    public const string TitlePrefix = ProductName + " — ";
}
