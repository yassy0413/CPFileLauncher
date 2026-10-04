using System.Diagnostics;
using Avalonia.Controls;
using FileLauncher.Core.Effects;

namespace FileLauncher.App;

/// <summary>
/// 常時の演出（光の玉・発光の明滅）を毎フレーム進める（spec/EFFECTS.md「常時の演出の詳細」）。
/// TopLevel.RequestAnimationFrame で動き、止まっている間はフレームを要求しない（CPU 0）。更新は 16 ms 以上の間隔。
/// </summary>
internal sealed class AmbientAnimator(TopLevel topLevel, OrbLayer orbs, PulseLayer pulse)
{
    private const double MinFrameMs = 16;
    private const double PulseStep = 0.02;
    private readonly Stopwatch _clock = new();
    private double _lastMs = double.NegativeInfinity;

    public bool IsRunning { get; private set; }

    /// <summary>明滅（glowPulse）。none なら Opacity は 0 のまま。</summary>
    public EffectSpec PulseSpec { get; set; } = EffectSpec.None;

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _clock.Start();
        _lastMs = double.NegativeInfinity;
        Tick(_clock.Elapsed.TotalMilliseconds);
        topLevel.RequestAnimationFrame(OnFrame);
    }

    public void Stop()
    {
        IsRunning = false;
        _clock.Stop(); // 再開したら続きから（玉が止まった位置から動き出す）
    }

    private void OnFrame(TimeSpan _)
    {
        if (!IsRunning) return; // Stop の後に遅れて来た 1 回
        double now = _clock.Elapsed.TotalMilliseconds;
        if (now - _lastMs >= MinFrameMs) Tick(now);
        topLevel.RequestAnimationFrame(OnFrame);
    }

    /// <summary>1 フレームぶん適用する（UI テストは時計を使わずこれで進める）。</summary>
    internal void Tick(double elapsedMs)
    {
        _lastMs = elapsedMs;
        orbs.Advance(elapsedMs);
        double peak = PulseSpec.Kind switch { EffectKind.Pulse => 0.45, EffectKind.PulseStrong => 0.9, _ => 0 };
        double level = peak <= 0 ? 0 : AmbientMath.PulseLevel(elapsedMs, PulseSpec.DurationMs, peak);
        // 明滅の層は盤面全体に掛かるので、変えるたびに盤面全体が描き直しになる。目に見える差（2%）が出たときだけ変える
        if (Math.Abs(level - pulse.Level) >= PulseStep || (level == 0) != (pulse.Level == 0)) pulse.Level = level;
    }
}
