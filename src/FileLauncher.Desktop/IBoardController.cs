using FileLauncher.Core.Model;

namespace FileLauncher.App;

/// <summary>
/// 表示モードごとの盤面の扱い（ポップアップ / 常駐。SPEC §3.4）。App が今のモードのコントローラだけを有効にする。
/// 両方とも起動時に一度だけイベントを購読し、無効の間は何もしない（付け替えのたびに購読し直さない）。
/// </summary>
internal interface IBoardController
{
    /// <summary>このモードを有効にする（常駐なら盤面を出す）。</summary>
    void Activate();

    /// <summary>無効にする（盤面を隠す。非表示の演出が終わるまで待てる）。</summary>
    Task DeactivateAsync();

    bool IsShown { get; }

    /// <summary>トレイのクリック。</summary>
    void Toggle(string source);

    /// <summary>トレイ / メニュー / 2 重起動から「盤面を表示」。</summary>
    void ShowFromExternal(string source);

    void Launch(LauncherItem item, IReadOnlyList<string>? droppedPaths);

    /// <summary>盤面の上にダイアログを出す間、閉じる条件を止める。</summary>
    Task<T> RunModalAsync<T>(Func<Task<T>> dialog);
}
