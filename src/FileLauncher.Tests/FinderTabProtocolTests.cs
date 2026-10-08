using FileLauncher.Core.Items;
using static FileLauncher.Core.Items.FinderTabProtocol;

namespace FileLauncher.Tests;

public class FinderTabProtocolTests
{
    [Theory]
    [InlineData("READY 3", Kind.Ready, 3)]
    [InlineData("  READY 1\n", Kind.Ready, 1)]
    [InlineData("(*READY 2*)", Kind.Ready, 2)]
    [InlineData("NOWINDOW", Kind.NoWindow, 0)]
    [InlineData("DONE", Kind.Done, 0)]
    [InlineData("TIMEOUT", Kind.Timeout, 0)]
    [InlineData("SELECTFAILED", Kind.SelectFailed, 0)]
    public void 握手の行を種類と数に読む(string line, Kind kind, int count)
    {
        Assert.Equal(new Line(kind, count), Parse(line));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ready 1")]
    [InlineData("READY")]
    [InlineData("READY x")]
    [InlineData("READY 0")]
    [InlineData("12:34: execution error: Finder got an error (-1743)")]
    public void 知らない行や壊れた行はOther(string? line)
    {
        Assert.Equal(Kind.Other, Parse(line).Kind);
    }
}
