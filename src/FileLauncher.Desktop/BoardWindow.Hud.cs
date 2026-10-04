using Avalonia.Threading;
using FileLauncher.Core.Hud;
using FileLauncher.Core.Model;

namespace FileLauncher.App;

/// <summary>盤面のステータス行（HUD 段階 A。SPEC §3.9）。時刻は分（または秒）の変わり目だけ書き換え、見えていない間は止める。</summary>
public partial class BoardWindow
{
    private readonly DispatcherTimer _clock = new();
    private bool _clockAttached;

    /// <summary>時刻のタイマーが動いているか（テスト用）。</summary>
    internal bool ClockRunning => _clock.IsEnabled;

    private void UpdateStatus()
    {
        if (_board.Pages.Count == 0) return;
        var mode = _appearance.Hud.StatusBar ? _appearance.Hud.Clock : ClockMode.Off;
        bool date = _appearance.Hud.StatusBar && _appearance.Hud.Date;
        var (page, items, time) = StatusLine.Format(_pageIndex, _board.Pages.Count, _board.Pages[_pageIndex].Items.Count, DateTime.Now, mode, date);
        StatusPage.Text = page;
        StatusItems.Text = items;
        StatusClock.Text = time ?? "";
        UpdateClock();
    }

    /// <summary>時刻のタイマーを動かす / 止める（見えている・収納中でない・時刻を出す設定のときだけ動かす）。</summary>
    private void UpdateClock()
    {
        if (!_clockAttached)
        {
            _clockAttached = true;
            _clock.Tick += (_, _) =>
            {
                UpdateStatus(); // 次の変わり目までの間隔は UpdateClock が付け直す
            };
        }
        var mode = _appearance?.Hud is { StatusBar: true } hud ? hud.Clock : ClockMode.Off;
        bool date = _appearance?.Hud is { StatusBar: true, Date: true };
        bool run = (mode != ClockMode.Off || date) && IsVisible && !_ambientSuspended;
        if (!run)
        {
            _clock.Stop();
            return;
        }
        // 次の分（秒）の変わり目に合わせる。少し遅らせて、変わり目の直前に発火して同じ時刻を書くのを避ける
        _clock.Interval = StatusLine.NextTick(DateTime.Now, mode, date) + TimeSpan.FromMilliseconds(20);
        if (!_clock.IsEnabled) _clock.Start();
    }
}
