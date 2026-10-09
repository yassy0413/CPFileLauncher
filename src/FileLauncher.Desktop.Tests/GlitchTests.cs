using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileLauncher.App;
using FileLauncher.Core.Effects;
using FileLauncher.Core.Model;

namespace FileLauncher.Desktop.Tests;

public sealed class GlitchTests
{
    private static (BoardWindow Board, Canvas Layer, Control Frame) ShowBoard(EffectSpec showEffect, EffectSpec? hideEffect = null, int intensity = 100)
    {
        var appearance = new AppearanceSettings { GlitchIntensity = intensity };
        appearance.Effects[EffectCatalog.BoardShow] = showEffect;
        if (hideEffect is not null) appearance.Effects[EffectCatalog.BoardHide] = hideEffect;
        var board = new BoardWindow();
        board.ApplyEffects(appearance);
        board.Render(Board.CreateDefault(), appearance, 0);
        board.PrepareShow();
        board.Show();
        Dispatcher.UIThread.RunJobs();
        var layer = board.GetVisualDescendants().OfType<Canvas>().Single(c => c.Name == "GlitchLayer");
        var frame = board.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Frame");
        return (board, layer, frame);
    }

    private static void Pump(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!task.IsCompleted && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void グリッチは静止画層を出して時間内に本物の盤面へ戻る()
    {
        var (board, layer, frame) = ShowBoard(new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 60, Easing = EasingKind.Linear });

        var task = board.PlayShowAsync(CancellationToken.None);
        Assert.True(layer.IsVisible);       // 最初のフレームから静止画層
        Assert.Equal(0, frame.Opacity);
        Assert.Equal(6, layer.Children.Count); // 影 2 + 本体 + 帯 3

        Pump(task);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(layer.IsVisible);
        Assert.Empty(layer.Children);
        Assert.Equal(1, frame.Opacity);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 強さ200パーセントでは影のずれが10pxで濃さが1_帯のずれが12から32px()
    {
        var (board, layer, _) = ShowBoard(new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 60, Easing = EasingKind.Linear }, intensity: 200);
        var task = board.PlayShowAsync(CancellationToken.None);
        var shadows = layer.Children.OfType<Border>().ToList();
        Assert.Equal(2, shadows.Count);
        Assert.Equal([-10.0, 10.0], shadows.Select(s => ((TranslateTransform)s.RenderTransform!).X).Order());
        Assert.All(shadows, s => Assert.Equal(1, s.Opacity));
        var bands = layer.Children.OfType<Image>().Skip(1).ToList(); // 先頭は本体
        Assert.Equal(3, bands.Count);
        Assert.All(bands, b => Assert.InRange(Math.Abs(((TranslateTransform)b.RenderTransform!).X), 12, 32));
        Pump(task);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 強さ0パーセントでは影も帯も作らず本体だけにフェードし_終わり方は同じ()
    {
        var (board, layer, frame) = ShowBoard(new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 60, Easing = EasingKind.Linear }, intensity: 0);
        var task = board.PlayShowAsync(CancellationToken.None);
        Assert.True(board.GlitchLook.IsStill);
        Assert.Single(layer.Children);
        Assert.IsType<Image>(layer.Children[0]);
        Pump(task);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(layer.IsVisible);
        Assert.Equal(1, frame.Opacity);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 表示のグリッチ中にフェードで閉じても静止画層が残らない()
    {
        var (board, layer, frame) = ShowBoard(new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 500, Easing = EasingKind.Linear },
            new EffectSpec { Kind = EffectKind.Fade, DurationMs = 60, Easing = EasingKind.EaseIn });
        using var cts = new CancellationTokenSource();
        _ = board.PlayShowAsync(cts.Token);
        Assert.True(layer.IsVisible);

        cts.Cancel();
        var hide = board.PlayHideAsync(CancellationToken.None);
        Assert.False(layer.IsVisible);
        Pump(hide);
        Assert.Empty(layer.Children);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 打ち切ったら表示状態に戻る()
    {
        var (board, layer, frame) = ShowBoard(new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 500, Easing = EasingKind.Linear });
        using var cts = new CancellationTokenSource();
        _ = board.PlayShowAsync(cts.Token);

        cts.Cancel();
        board.ResetFrame();

        Assert.False(layer.IsVisible);
        Assert.Equal(1, frame.Opacity);
        board.AllowClose = true;
        board.Close();
    }

    [AvaloniaFact]
    public void 非表示のグリッチは静止画層を出し_終わると盤面全体が透明になる()
    {
        var glitch = new EffectSpec { Kind = EffectKind.Glitch, DurationMs = 60, Easing = EasingKind.Linear };
        var (board, layer, frame) = ShowBoard(EffectSpec.None, glitch);
        board.PlayShowAsync(CancellationToken.None);
        var root = board.GetVisualDescendants().OfType<FrameChrome>().Single();

        var hide = board.PlayHideAsync(CancellationToken.None);
        Assert.True(layer.IsVisible);
        Pump(hide);

        Assert.True(hide.IsCompletedSuccessfully);
        Assert.False(layer.IsVisible);
        Assert.Equal(0, root.Opacity);
        board.AllowClose = true;
        board.Close();
    }
}
