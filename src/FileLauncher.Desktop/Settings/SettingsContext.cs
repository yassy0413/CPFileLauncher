using Avalonia;
using FileLauncher.Core.Storage;
using FileLauncher.Platform;

namespace FileLauncher.App;

/// <summary>設定画面が使うもの一式（App が用意する）。</summary>
internal sealed record SettingsContext(
    SettingsHub Hub,
    IPlatformServices Platform,
    DataPaths Paths,
    Func<PixelPoint?> BoardPosition,
    Action<string> OpenFolder,
    Action ShowPermissionGuide,
    Action? FlushAll = null,                          // エクスポートの前に保存待ちを書き出す
    Action<ArchiveContents>? ImportAndRestart = null, // 差し替えて再起動（spec/SETTINGS.md データタブ）
    Func<BackgroundLoadStatus>? BackgroundStatus = null,             // 背景画像の読み込み結果（注記用）
    Func<Avalonia.Media.Imaging.Bitmap?>? BackgroundBitmap = null,  // 背景画像のプレビュー（読み込み済みのもの）
    Action? Restart = null);                                         // 「今すぐ再起動」（言語）
