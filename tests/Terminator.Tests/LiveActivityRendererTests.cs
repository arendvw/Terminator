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

    [Fact]
    public void BuildFullScreen_FillsTheWholeConsoleEvenWithLittleContent()
    {
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        var lines = RenderFullScreen(table);

        Assert.Equal(Height, lines.Count);
        Assert.All(lines, line => Assert.Equal(Width, line.CellCount()));
    }

    [Fact]
    public void BuildFullScreen_StatusColumnTakesWhatItNeedsAndTheLogTheRest()
    {
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        var logPanelStart = LogPanelStart(RenderFullScreen(table));

        Assert.InRange(logPanelStart, 10, Width / 2 - 1);
    }

    [Fact]
    public void BuildFullScreen_StatusColumnLeavesRoomForTheLog()
    {
        using var table = new ActivityObservationTable();
        table.Start("build", "Building").Report(0.5, new string('x', 400));

        var logPanelStart = LogPanelStart(RenderFullScreen(table));

        Assert.InRange(logPanelStart, Width / 2, Width * 3 / 5);
    }

    [Fact]
    public void BuildFullScreen_WrapsLongLogLinesInsteadOfClipping()
    {
        using var table = new ActivityObservationTable();
        var scope = table.Start("deploy", "Deploying");
        var words = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"word{i:00}"));
        scope.Log(CliLogLevel.Information, words + " END-OF-LINE");

        var text = string.Join("\n", RenderFullScreen(table).Select(Text));

        Assert.Contains("word00", text);
        Assert.Contains("word59", text);
        Assert.Contains("END-OF-LINE", text);
    }

    [Fact]
    public void BuildFullScreen_KeepsTheNewestLogLinesThatFit()
    {
        using var table = new ActivityObservationTable();
        var scope = table.Start("migrate", "Migrating");
        for (var i = 0; i < 200; i++)
            scope.Log(CliLogLevel.Information, $"DO {i:000}");

        var lines = RenderFullScreen(table);
        var text = string.Join("\n", lines.Select(Text));

        Assert.Equal(Height, lines.Count);
        Assert.Contains("DO 199", text);
        Assert.DoesNotContain("DO 000", text);
        // every row between the borders of the log panel shows a log line
        Assert.Equal(Height - 2, lines.Count(l => Text(l).Contains("DO ")));
    }

    [Fact]
    public void SupportsFullScreen_OnlyOnAnInteractiveAnsiTerminal()
    {
        Assert.True(LiveActivityRenderer.SupportsFullScreen(CreateConsole(ansi: true, interactive: true), isTerminal: true));
        Assert.False(LiveActivityRenderer.SupportsFullScreen(CreateConsole(ansi: true, interactive: true), isTerminal: false));
        Assert.False(LiveActivityRenderer.SupportsFullScreen(CreateConsole(ansi: false, interactive: true), isTerminal: true));
        Assert.False(LiveActivityRenderer.SupportsFullScreen(CreateConsole(ansi: true, interactive: false), isTerminal: true));
    }

    [Fact]
    public async Task RunAsync_RedirectedOutputStaysInline()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        await RunAsync(console, table, isTerminal: false);

        Assert.DoesNotContain(EnterAlternateScreen, output.ToString());
        Assert.Contains("Building", output.ToString());
    }

    [Fact]
    public async Task RunAsync_FullScreenOptOutStaysInline()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        await RunAsync(console, table, isTerminal: true, fullScreen: false);

        Assert.DoesNotContain(EnterAlternateScreen, output.ToString());
        Assert.Contains("Building", output.ToString());
    }

    [Fact]
    public async Task RunAsync_FullScreenUsesTheAlternateScreenAndLeavesTheResultBehind()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var table = new ActivityObservationTable();
        table.Start("build", "Building").Stop();

        await RunAsync(console, table, isTerminal: true);

        var text = output.ToString();
        var enter = text.IndexOf(EnterAlternateScreen, StringComparison.Ordinal);
        var leave = text.IndexOf(LeaveAlternateScreen, StringComparison.Ordinal);
        Assert.Equal(0, enter);
        Assert.True(leave > enter);
        Assert.Contains("Building", text[leave..]);
    }

    [Fact]
    public async Task RunAsync_SecondTrackerGetsAFreshScreen()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var stage = new ActivityObservationTable();
        stage.Start("stage", "Staging").Stop();
        using var publish = new ActivityObservationTable();
        publish.Start("publish", "Publishing").Stop();

        await RunAsync(console, stage, isTerminal: true);
        var first = output.ToString().Length;
        await RunAsync(console, publish, isTerminal: true);

        var second = output.ToString()[first..];
        Assert.StartsWith(EnterAlternateScreen + ClearScreen, second);
        var leave = second.IndexOf(LeaveAlternateScreen, StringComparison.Ordinal);
        Assert.DoesNotContain("Staging", second);
        Assert.Contains("Publishing", second[leave..]);
    }

    [Fact]
    public async Task RunAsync_RedrawsAndClearsOnResize()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        await RunAsync(console, table, isTerminal: true, during: renderer =>
        {
            var before = output.ToString().Length;
            renderer.RefreshNow();
            Assert.DoesNotContain(ClearScreen, output.ToString()[before..]);

            console.Profile.Width = Width - 20;
            console.Profile.Height = Height - 5;
            before = output.ToString().Length;
            renderer.RefreshNow();
            var frame = output.ToString()[before..];
            Assert.StartsWith(ClearScreen, frame);
            Assert.Contains($"\x1b[{Height - 5};1H", frame);
            Assert.DoesNotContain($"\x1b[{Height - 4};1H", frame);
        });
    }

    [Fact]
    public async Task RunAsync_OutputWrittenDuringTheDisplayEndsUpOnTheNormalScreen()
    {
        var output = new StringWriter();
        var console = CreateConsole(ansi: true, interactive: true, output);
        using var table = new ActivityObservationTable();
        table.Start("build", "Building");

        await RunAsync(console, table, isTerminal: true, during: renderer =>
        {
            console.WriteLine("written meanwhile");
            renderer.RefreshNow();
        });

        var text = output.ToString();
        var leave = text.IndexOf(LeaveAlternateScreen, StringComparison.Ordinal);
        Assert.Contains("written meanwhile", text[..leave]); // shown in the log pane
        var after = text[leave..];
        var replayed = after.IndexOf("written meanwhile", StringComparison.Ordinal);
        Assert.InRange(replayed, 0, after.IndexOf("Building", StringComparison.Ordinal));
    }

    private const string EnterAlternateScreen = "\x1b[?1049h";
    private const string LeaveAlternateScreen = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";

    private static async Task RunAsync(IAnsiConsole console, ActivityObservationTable table, bool isTerminal,
        Action<LiveActivityRenderer>? during = null, bool fullScreen = true)
    {
        using var renderer = new LiveActivityRenderer(console, interval: null)
        {
            IsTerminalOverride = isTerminal,
            FullScreen = fullScreen,
        };
        using var cts = new CancellationTokenSource();
        var run = renderer.RunAsync(table, cts.Token);
        during?.Invoke(renderer);
        await cts.CancelAsync();
        await run;
    }

    private static string Text(SegmentLine line) => string.Concat(line.Select(s => s.Text));

    /// <summary>The column where the log panel starts: the second top-left corner on the first row.</summary>
    private static int LogPanelStart(List<SegmentLine> lines)
    {
        var top = Text(lines[0]);
        return top.IndexOf(top[0], 1);
    }

    private static List<SegmentLine> RenderFullScreen(ActivityObservationTable table)
    {
        var console = CreateConsole();
        using var renderer = new LiveActivityRenderer(console, interval: null);
        var options = RenderOptions.Create(console, console.Profile.Capabilities);
        return Segment.SplitLines(renderer.BuildFullScreen(table).Render(options, Width));
    }

    private static List<SegmentLine> Render(ActivityObservationTable table)
    {
        var console = CreateConsole();
        using var renderer = new LiveActivityRenderer(console, interval: null);
        var options = RenderOptions.Create(console, console.Profile.Capabilities);
        return Segment.SplitLines(renderer.Build(table).Render(options, Width));
    }

    private static IAnsiConsole CreateConsole(bool ansi = false, bool interactive = false, StringWriter? output = null)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = ansi ? AnsiSupport.Yes : AnsiSupport.No,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(output ?? new StringWriter()),
        });
        console.Profile.Width = Width;
        console.Profile.Height = Height;
        return console;
    }
}
