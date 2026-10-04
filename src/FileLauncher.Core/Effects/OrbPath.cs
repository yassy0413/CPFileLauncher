namespace FileLauncher.Core.Effects;

/// <summary>
/// 角丸矩形の周上の経路（光の玉 frameOrb。spec/EFFECTS.md「常時の演出の詳細」）。
/// 始点は左上の角の終わり（上辺の始まり）、時計回り。距離は周長で巻く（負も可）。
/// </summary>
public sealed class OrbPath
{
    private readonly double _w, _h, _r;
    private readonly double[] _segments; // 上辺, 右上弧, 右辺, 右下弧, 下辺, 左下弧, 左辺, 左上弧

    public OrbPath(double width, double height, double cornerRadius)
    {
        _w = Math.Max(0, width);
        _h = Math.Max(0, height);
        _r = Math.Clamp(cornerRadius, 0, Math.Min(_w, _h) / 2);
        double arc = Math.PI * _r / 2;
        double hx = _w - 2 * _r, vy = _h - 2 * _r;
        _segments = [hx, arc, vy, arc, hx, arc, vy, arc];
        Length = _segments.Sum();
    }

    public double Length { get; }

    public (double X, double Y) PointAt(double distance)
    {
        if (Length <= 0) return (0, 0);
        double d = distance % Length;
        if (d < 0) d += Length;
        double r = _r;
        for (int i = 0; i < _segments.Length; i++)
        {
            double len = _segments[i];
            if (d > len && i < _segments.Length - 1) { d -= len; continue; }
            double t = len <= 0 ? 0 : Math.Clamp(d / len, 0, 1);
            return i switch
            {
                0 => (r + t * len, 0),
                1 => Arc(_w - r, r, -90 + 90 * t),
                2 => (_w, r + t * len),
                3 => Arc(_w - r, _h - r, 0 + 90 * t),
                4 => (_w - r - t * len, _h),
                5 => Arc(r, _h - r, 90 + 90 * t),
                6 => (0, _h - r - t * len),
                _ => Arc(r, r, 180 + 90 * t),
            };
        }
        return (r, 0);
    }

    private (double X, double Y) Arc(double cx, double cy, double degrees)
    {
        double a = degrees * Math.PI / 180;
        return (cx + _r * Math.Cos(a), cy + _r * Math.Sin(a));
    }
}
