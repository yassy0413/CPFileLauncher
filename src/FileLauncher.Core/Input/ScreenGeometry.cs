namespace FileLauncher.Core.Input;

/// <summary>画面座標（物理 px）。Windows のフック座標・Avalonia の PixelPoint と同じ単位。</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>画面上の矩形（物理 px）。</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(ScreenPoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
}
