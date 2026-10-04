using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FileLauncher.Core.Effects;

namespace FileLauncher.App;

/// <summary>
/// ウィンドウの位置を時間をかけて動かす（演出 residentAutoHide / residentMove。spec/EFFECTS.md）。
/// Window.Position は Transitions で補間できないので、約 60 fps のタイマーでイージングに沿って動かす。
/// slide 以外・時間 0 は即座に移動する。
/// </summary>
internal static class WindowMover
{
    public static async Task MoveAsync(Window window, PixelPoint target, EffectSpec spec, CancellationToken ct = default)
    {
        var from = window.Position;
        if (spec.Kind != EffectKind.Slide || !spec.IsActive || from == target)
        {
            window.Position = target;
            return;
        }

        var easing = EffectRunner.ToEasing(spec.Easing);
        var sw = Stopwatch.StartNew();
        var done = new TaskCompletionSource();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            if (ct.IsCancellationRequested)
            {
                timer.Stop();
                done.TrySetResult();
                return;
            }
            double t = Math.Min(1, sw.Elapsed.TotalMilliseconds / spec.DurationMs);
            double k = easing.Ease(t);
            window.Position = new PixelPoint(
                (int)Math.Round(from.X + (target.X - from.X) * k),
                (int)Math.Round(from.Y + (target.Y - from.Y) * k));
            if (t >= 1)
            {
                timer.Stop();
                done.TrySetResult();
            }
        };
        timer.Start();
        await done.Task;
    }
}
