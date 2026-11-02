using Spectre.Console;
using Spectre.Console.Rendering;
using Terminator.ActivityObserver;

namespace Terminator.Tests;

public class LiveActivityRendererTests
{
    private const int Width = 120;
    private const int Height = 30;

    [Theory]
    [InlineData("a\tb", "a   b")]
    [InlineData("abcd\te", "abcd    e")]
    [InlineData("x\n\ty", "x\n    y")]
    public void Sanitize_ExpandsTabsToTabStops(string input, string expected)
    {
        Assert.Equal(expected, LiveActivityRenderer.Sanitize(input));
    }

    [Theory]
    [InlineData("\x1b[31mred\x1b[0m", "red")]
    [InlineData("\x1b]0;title\x07text", "text")]
    [InlineData("\x1b]8;;https://example.com\x1b\\link\x1b]8;;\x1b\\", "link")]
    [InlineData("line\r\n", "line\n")]
    [InlineData("bell\a back\b", "bell back")]
    public void Sanitize_StripsEscapesAndControlCharacters(string input, string expected)
    {
        Assert.Equal(expected, LiveActivityRenderer.Sanitize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_ReturnsEmptyForNoText(string? input)
    {
        Assert.Equal(string.Empty, LiveActivityRenderer.Sanitize(input));
    }

    [Fact]
    public void Pad_PadsShortTextToLength()
    {
        using var renderer = new LiveActivityRenderer(CreateConsole(), interval: null);

        Assert.Equal("abc       ", renderer.Pad("abc", 10));
        Assert.Equal("          ", renderer.Pad(null, 10));
    }

    [Fact]
    public void Pad_TruncatesLongTextWithEllipsis()
    {
        using var renderer = new LiveActivityRenderer(CreateConsole(), interval: null);

        Assert.Equal("abcdefghi…", renderer.Pad("abcdefghijklmnop", 10));
    }

    [Fact]
    public void Build_FrameHeightFollowsContent()
    {
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        var lines = Render(table);

        Assert.InRange(lines.Count, 1, 10);
    }

    [Fact]
    public void Build_FrameLeavesAFreeLineWhenContentIsTall()
    {
        using var table = new ActivityObservationTable();
        var scopes = Enumerable.Range(0, 40).Select(i => table.Start($"step{i}", $"Step {i}")).ToList();
        for (var i = 0; i < 40; i++)
            scopes[0].Log(CliLogLevel.Information, $"line {i}");

        var lines = Render(table);

        Assert.Equal(Height - 1, lines.Count);
    }

    [Fact]
    public void Build_UnsafeTextNeverRendersWiderThanTheConsole()
    {
        using var table = new ActivityObservationTable();
        var scope = table.Start("copy", "Copying");
        scope.Log(CliLogLevel.Information, "\t\tNew File\t\t  1.2 m\tC:\\some\\very\\long\\path\\to\\a\\file.bin\t\t100%");
        scope.Log(CliLogLevel.Information, "\x1b[32mgreen\x1b[0m [not markup] [/] \x1b]0;title\x07");
        scope.Log(CliLogLevel.Information, "wide: 你好世界你好世界你好世界你好世界");
        scope.Report(0.5, "[1/3] copying [red]C:\\path\\with\\brackets[/]\t\x1b[1mbold\x1b[0m");

        var lines = Render(table);

        // Spectre measures tabs, escapes and control characters as one cell or less, the terminal does not.
        // Without them in the output, Spectre's cell count is what the terminal shows.
        Assert.All(lines, line =>
        {
            var text = string.Concat(line.Select(s => s.Text));
            Assert.DoesNotContain(text, char.IsControl);
            Assert.True(line.CellCount() <= Width, $"line is {line.CellCount()} cells wide: {text}");
        });
    }

    [Fact]
    public void Build_LongProgressMessageStaysOnOneRow()
    {
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");
        var shortFrame = Render(table).Count;

        using var longTable = new ActivityObservationTable();
        longTable.Start("build", "Building")
            .Report(0.5, "copying C:\\a\\very\\long\\path\\that\\would\\wrap\\in\\the\\progress\\column\\file.bin");
        var lines = Render(longTable).Select(l => string.Concat(l.Select(s => s.Text))).ToList();

        Assert.Equal(shortFrame, lines.Count);
        Assert.Contains(lines, l => l.Contains("Building") && l.Contains("copying") && l.Contains('…'));
    }

    private static List<SegmentLine> Render(ActivityObservationTable table)
    {
        var console = CreateConsole();
        using var renderer = new LiveActivityRenderer(console, interval: null);
        var options = RenderOptions.Create(console, console.Profile.Capabilities);
        return Segment.SplitLines(renderer.Build(table).Render(options, Width));
    }

    private static IAnsiConsole CreateConsole()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(new StringWriter()),
        });
        console.Profile.Width = Width;
        console.Profile.Height = Height;
        return console;
    }
}
