using FileLauncher.Core.Model;
using FileLauncher.Core.Theming;

namespace FileLauncher.Tests;

/// <summary>背景画像の表示方法 5 種の意味（SPEC §3.7「表示方法の意味」）。</summary>
public class BackgroundLayoutTests
{
    [Fact]
    public void 埋めるは面を覆う倍率で元画像の中央を切り出して面全体に描く()
    {
        var p = BackgroundLayout.Compute(200, 100, 400, 400, BackgroundFit.Fill); // 倍率 0.5 → 元画像の 400×200 を使う
        Assert.Equal((0.0, 100.0, 400.0, 200.0), p.Source);
        Assert.Equal((0.0, 0.0, 200.0, 100.0), p.Dest);
        Assert.Equal((1, 1), (p.TileColumns, p.TileRows));
    }

    [Fact]
    public void 収めるは画像全体を面の中央に縮めて描く()
    {
        var p = BackgroundLayout.Compute(200, 100, 400, 400, BackgroundFit.Fit); // 倍率 0.25 → 100×100
        Assert.Equal((0.0, 0.0, 400.0, 400.0), p.Source);
        Assert.Equal((50.0, 0.0, 100.0, 100.0), p.Dest);
    }

    [Fact]
    public void 引き伸ばしは画像全体を面全体に描く()
    {
        var p = BackgroundLayout.Compute(200, 100, 40, 40, BackgroundFit.Stretch);
        Assert.Equal((0.0, 0.0, 40.0, 40.0), p.Source);
        Assert.Equal((0.0, 0.0, 200.0, 100.0), p.Dest);
    }

    [Fact]
    public void 中央は原寸で_面より大きい辺は元画像を中央で切る()
    {
        var small = BackgroundLayout.Compute(200, 100, 40, 20, BackgroundFit.Center);
        Assert.Equal((0.0, 0.0, 40.0, 20.0), small.Source);
        Assert.Equal((80.0, 40.0, 40.0, 20.0), small.Dest);
        var wide = BackgroundLayout.Compute(200, 100, 300, 20, BackgroundFit.Center);
        Assert.Equal((50.0, 0.0, 200.0, 20.0), wide.Source);
        Assert.Equal((0.0, 40.0, 200.0, 20.0), wide.Dest);
    }

    [Fact]
    public void 並べるは原寸を左上から面を覆う枚数だけ並べる()
    {
        var p = BackgroundLayout.Compute(200, 100, 64, 32, BackgroundFit.Tile);
        Assert.Equal((0.0, 0.0, 64.0, 32.0), p.Dest);
        Assert.Equal((4, 4), (p.TileColumns, p.TileRows));
    }

    [Fact]
    public void 面か画像の辺が0なら何も描かない()
    {
        Assert.True(BackgroundLayout.Compute(0, 100, 64, 32, BackgroundFit.Fill).IsEmpty);
        Assert.True(BackgroundLayout.Compute(200, 100, 64, 0, BackgroundFit.Tile).IsEmpty);
    }
}
