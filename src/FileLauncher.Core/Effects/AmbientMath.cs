namespace FileLauncher.Core.Effects;

/// <summary>常時の演出の計算（spec/EFFECTS.md「常時の演出の詳細」）。</summary>
public static class AmbientMath
{
    /// <summary>経過時間 → 周期の中の位置（0 以上 1 未満）。周期 0 以下なら 0。</summary>
    public static double Phase(double elapsedMs, int periodMs)
    {
        if (periodMs <= 0) return 0;
        double p = elapsedMs % periodMs / periodMs;
        return p < 0 ? p + 1 : p;
    }

    /// <summary>明滅の強さ（0〜peak）。サイン波（0 から始まり半周期で peak）。</summary>
    public static double PulseLevel(double elapsedMs, int periodMs, double peak) =>
        peak * (1 - Math.Cos(2 * Math.PI * Phase(elapsedMs, periodMs))) / 2;
}
