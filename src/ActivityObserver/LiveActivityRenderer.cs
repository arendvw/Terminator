using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;
using Timer = System.Timers.Timer;

namespace Terminator.ActivityObserver;

/// <summary>
/// Render a live updatable progress of each activity step.
/// </summary>
/// <remarks>
/// On an interactive ANSI terminal the display takes over the whole window on the alternate screen
/// buffer, redraws on resize, and when it stops returns to the normal screen and writes the final
/// state there once. Anything written to the console meanwhile is shown in the log pane and written
/// to the normal screen afterwards. Elsewhere (redirected output, CI logs, consoles without ANSI
/// support) it falls back to an inline live display that is only as high as its content.
/// </remarks>
public sealed class LiveActivityRenderer : IDisposable
{
    private const string EnterAlternateScreen = "\x1b[?1049h\x1b[2J\x1b[H\x1b[?25l";
    private const string LeaveAlternateScreen = "\x1b[?25h\x1b[?1049l";
    private const int InlineLogLines = 20;
    private const int PanelChrome = 4; // border and padding on both sides of a panel

    private readonly IAnsiConsole _console;
    private Action? _unsubscribe;
    private readonly Spinner _spinner = Spinner.Known.Default;
    private Timer? _timer;
    private event Action<object>? Refresh;

    private readonly object _drawLock = new();
    private readonly List<(DateTime TimeUtc, IRenderable Renderable)> _captured = [];
    private bool _onAlternateScreen;
    private (int Width, int Height) _screenSize;
    private int _statusWidth;
    private int _statusWidthFor;
    private int _frames;

    public LiveActivityRenderer(IAnsiConsole console, int? interval = 100)
    {
        if (interval != null)
        {
            _timer = new Timer(interval.Value);
            _timer.Elapsed += (_, __) =>
            {
                Refresh?.Invoke(() => DateTime.UtcNow);
            };
            _timer.Start();
        }
        _console = console;
    }

    /// <summary>
    /// Use the whole terminal window (alternate screen buffer) when the console is an interactive
    /// ANSI terminal. Defaults to <c>true</c>; set to <c>false</c> to always render inline.
    /// </summary>
    public bool FullScreen { get; set; } = true;

    /// <summary>Test seam: overrides whether the console output is a terminal.</summary>
    internal bool? IsTerminalOverride { get; set; }

    public async Task RunAsync(ActivityObservationTable table, CancellationToken token)
    {
        if (FullScreen && SupportsFullScreen(_console, IsTerminalOverride ?? _console.Profile.Out.IsTerminal))
        {
            await RunFullScreenAsync(table, token);
        }
        else
        {
            await RunInlineAsync(table, token);
        }
    }

    /// <summary>
    /// Whether the alternate screen can be used: it needs ANSI escape sequences, a user at the
    /// console, and output that goes to the terminal rather than to a file or pipe.
    /// </summary>
    internal static bool SupportsFullScreen(IAnsiConsole console, bool isTerminal) =>
        isTerminal && console.Profile.Capabilities.Ansi && console.Profile.Capabilities.Interactive;

    private async Task RunInlineAsync(ActivityObservationTable table, CancellationToken token)
    {
        var target = Build(table);

        await _console.Live(target)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // subscribe to updates; on any event, rebuild + refresh
                _unsubscribe = Subscribe(table, () =>
                {
                    _frames++;
                    ctx.UpdateTarget(Build(table, _frames));
                    ctx.Refresh();
                });

                try
                {
                    // block until cancelled
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    /* swallow */
                }
                finally
                {
                    ctx.UpdateTarget(Build(table, _frames));
                    ctx.Refresh();
                    _unsubscribe?.Invoke();
                    _unsubscribe = null;
                }
            });
    }

    private async Task RunFullScreenAsync(ActivityObservationTable table, CancellationToken token)
    {
        // Exclusive like Spectre's own live display: a prompt meanwhile fails instead of being hidden.
        await _console.RunExclusive(async () =>
        {
            var hook = new CaptureHook(this);
            lock (_drawLock)
            {
                _captured.Clear();
                _screenSize = default;
                _console.Write(new ControlCode(EnterAlternateScreen));
                _onAlternateScreen = true;
                AppDomain.CurrentDomain.ProcessExit += RestoreScreenOnExit;
                _console.Pipeline.Attach(hook);
            }

            try
            {
                Draw(table);
                _unsubscribe = Subscribe(table, () => Draw(table));
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                /* swallow */
            }
            finally
            {
                _unsubscribe?.Invoke();
                _unsubscribe = null;

                List<(DateTime TimeUtc, IRenderable Renderable)> captured;
                lock (_drawLock)
                {
                    _console.Pipeline.Detach(hook);
                    _onAlternateScreen = false;
                    AppDomain.CurrentDomain.ProcessExit -= RestoreScreenOnExit;
                    _console.Write(new ControlCode(LeaveAlternateScreen));
                    captured = [.. _captured];
                    _captured.Clear();
                }

                // The alternate screen has no scrollback: write what happened to the normal screen once.
                foreach (var (_, renderable) in captured)
                    _console.Write(renderable);
                _console.Write(Build(table, _frames));
            }

            return 0;
        });
    }

    /// <summary>Redraw the live display now, as the refresh timer does.</summary>
    internal void RefreshNow() => Refresh?.Invoke(DateTime.UtcNow);

    private void Draw(ActivityObservationTable table)
    {
        // Skip a tick while the previous frame is still being written.
        if (!Monitor.TryEnter(_drawLock))
            return;
        try
        {
            if (!_onAlternateScreen)
                return;

            var size = (_console.Profile.Width, _console.Profile.Height);
            var resized = size != _screenSize;
            _screenSize = size;
            _frames++;
            _console.Write(new ScreenFrame(BuildFullScreen(table, _frames), resized));
        }
        finally
        {
            Monitor.Exit(_drawLock);
        }
    }

    private void RestoreScreenOnExit(object? sender, EventArgs e)
    {
        if (_onAlternateScreen)
        {
            _onAlternateScreen = false;
            _console.Write(new ControlCode(LeaveAlternateScreen));
        }
    }

    private void Capture(IRenderable renderable)
    {
        lock (_captured)
            _captured.Add((DateTime.UtcNow, renderable));
    }

    public bool HasChange { get; set; }

    private Action Subscribe(ActivityObservationTable table, Action onChanged)
    {
        Refresh += Update;
        return () =>
        {
            Refresh -= Update;
        };

        void Update(object _) => onChanged();
    }

    private string SpinnerFrame(int frameCount)
    {
        var frame = frameCount % _spinner.Frames.Count;
        return $"{_spinner.Frames[frame]}";
    }

    /// <summary>
    /// The inline display: as high as its content (at most the console height minus one line,
    /// so output written meanwhile does not scroll it), with the last log lines.
    /// </summary>
    internal IRenderable Build(ActivityObservationTable table, int frameCount = 0) =>
        new ActivityFrame(this, table, frameCount, fullScreen: false);

    /// <summary>The full-screen display: exactly the console width and height.</summary>
    internal IRenderable BuildFullScreen(ActivityObservationTable table, int frameCount = 0) =>
        new ActivityFrame(this, table, frameCount, fullScreen: true);

    private Table BuildStatusTable(ActivityObservationTable table, int frameCount)
    {
        var frame = SpinnerFrame(frameCount);
        var t = new Table()
            .Border(TableBorder.Simple)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("")
            .AddColumn("")
            .AddColumn("");

        foreach (var e in table.SnapshotOrderedByStartUtc())
        {
            var progress = e.LastProgress.HasValue
                ? $"{e.LastProgress:P0}"
                : "";

            var duration = (e.EndedUtc - e.StartedUtc)?.TotalSeconds;
            t.AddRow(
                StatusIcon(e.StartedUtc != null, e.EndedUtc != null, frame),
                new Markup(e.Description ?? ""),
                new SingleLine(Sanitize(e.LastProgressMessage).Replace('\n', ' ')),
                new Markup(e.EndedUtc == null ? progress : $"{duration:0.0}s")
            );
        }

        return t;
    }

    /// <summary>
    /// The width of the status column: what the step table needs, at most three fifths of the
    /// console so the log keeps room. It only grows while the console width stays the same, so
    /// the columns do not shift back and forth as progress messages change length.
    /// </summary>
    private int StatusWidth(IRenderable statusTable, RenderOptions options, int maxWidth)
    {
        var maxStatus = Math.Max(PanelChrome + 1, maxWidth * 3 / 5);
        var needed = Math.Min(maxStatus, statusTable.Measure(options, maxStatus - PanelChrome).Max + PanelChrome);
        lock (_drawLock)
        {
            if (_statusWidthFor != maxWidth)
            {
                _statusWidthFor = maxWidth;
                _statusWidth = 0;
            }
            _statusWidth = Math.Min(maxStatus, Math.Max(_statusWidth, needed));
            return _statusWidth;
        }
    }

    /// <summary>
    /// The newest log lines that fit, oldest first: every message wrapped to the column width
    /// (never clipped), including output other code wrote to the console during the display.
    /// </summary>
    internal List<SegmentLine> LogLines(ActivityObservationTable table, RenderOptions options, int width, int maxLines)
    {
        var lines = new List<SegmentLine>();
        if (width <= 0 || maxLines <= 0)
            return lines;

        List<(DateTime TimeUtc, IRenderable Renderable)> captured;
        lock (_captured)
            captured = [.. _captured];

        var entries = table.LatestLogs
            .Select(e => (e.TimeUtc, Renderable: (IRenderable)new Text($"{e.TimeUtc:HH:mm:ss}: {Sanitize(e.Message)}")))
            .Concat(captured)
            .OrderByDescending(e => e.TimeUtc);

        // newest first, so only what fits is rendered
        foreach (var (_, renderable) in entries)
        {
            var rendered = Segment.SplitLines(renderable.Render(options with { Height = null }, width), width);
            for (var i = rendered.Count - 1; i >= 0; i--)
            {
                lines.Add(rendered[i]);
                if (lines.Count == maxLines)
                {
                    lines.Reverse();
                    return lines;
                }
            }
        }

        lines.Reverse();
        return lines;
    }

    /// <summary>
    /// Status and log side by side. The status column gets the width its table needs, the log
    /// the rest. Full screen it is exactly the console height; inline it follows the content.
    /// </summary>
    private sealed class ActivityFrame(LiveActivityRenderer renderer, ActivityObservationTable table, int frameCount, bool fullScreen) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth) => new(maxWidth, maxWidth);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var statusTable = renderer.BuildStatusTable(table, frameCount);
            var statusWidth = renderer.StatusWidth(statusTable, options, maxWidth);
            var logWidth = Math.Max(0, maxWidth - statusWidth);

            var consoleHeight = options.ConsoleSize.Height;
            var maxHeight = Math.Max(1, fullScreen ? consoleHeight : consoleHeight - 1);
            var maxLogLines = Math.Max(0, maxHeight - 2);
            if (!fullScreen)
                maxLogLines = Math.Min(InlineLogLines, maxLogLines);
            var logLines = renderer.LogLines(table, options, logWidth - PanelChrome, maxLogLines);

            var statusPanel = new Panel(statusTable).Expand();
            var logPanel = new Panel(new Lines(logLines)).Expand();

            var height = maxHeight;
            if (!fullScreen)
            {
                var statusHeight = Segment.SplitLines(((IRenderable)statusPanel).Render(options with { Height = null }, statusWidth)).Count;
                height = Math.Max(1, Math.Min(Math.Max(statusHeight, logLines.Count + 2), maxHeight));
            }

            var layout = new Layout("Root").SplitColumns(
                new Layout("Status", statusPanel).Size(statusWidth),
                new Layout("Log", logPanel));
            return ((IRenderable)layout).Render(options with { Height = height }, maxWidth);
        }
    }

    /// <summary>
    /// Lines rendered beforehand, written as they are.
    /// </summary>
    private sealed class Lines(IReadOnlyList<SegmentLine> lines) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            var width = lines.Select(l => l.CellCount()).DefaultIfEmpty(0).Max();
            return new Measurement(Math.Min(1, width), Math.Min(width, maxWidth));
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    yield return Segment.LineBreak;
                foreach (var segment in lines[i])
                    yield return segment;
            }
        }
    }

    /// <summary>
    /// One full-screen frame. Every line is positioned explicitly, so neither the way the terminal
    /// handles line breaks nor a full last line can scroll the screen. After a resize the screen
    /// is cleared first, so nothing of a frame drawn at the old size stays behind.
    /// </summary>
    private sealed class ScreenFrame(IRenderable frame, bool clear) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth) => new(maxWidth, maxWidth);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var height = options.ConsoleSize.Height;
            var lines = Segment.SplitLines(frame.Render(options, maxWidth), maxWidth, height);
            if (clear)
                yield return Segment.Control("\x1b[2J");
            for (var i = 0; i < lines.Count && i < height; i++)
            {
                yield return Segment.Control($"\x1b[{i + 1};1H");
                foreach (var segment in lines[i])
                    yield return segment;
            }
        }
    }

    /// <summary>
    /// While the display is on the alternate screen, keep other writes to the console from landing
    /// in it (where they would be overdrawn and lost); they are shown in the log pane instead and
    /// written to the normal screen when the display stops.
    /// </summary>
    private sealed class CaptureHook(LiveActivityRenderer renderer) : IRenderHook
    {
        public IEnumerable<IRenderable> Process(RenderOptions options, IEnumerable<IRenderable> renderables)
        {
            var passed = new List<IRenderable>();
            foreach (var renderable in renderables)
            {
                if (renderable is ScreenFrame or ControlCode)
                    passed.Add(renderable);
                else
                    renderer.Capture(renderable);
            }
            return passed;
        }
    }

    /// <summary>
    /// Plain text that never wraps: it is cut off with an ellipsis at the width its table column gets,
    /// so a long progress message cannot add rows and change the height of the display.
    /// </summary>
    private sealed class SingleLine(string text) : IRenderable
    {
        private readonly Segment _segment = new(text);

        public Measurement Measure(RenderOptions options, int maxWidth) =>
            new(Math.Min(1, _segment.CellCount()), Math.Min(_segment.CellCount(), maxWidth));

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            if (_segment.CellCount() <= maxWidth)
                return [_segment];
            if (maxWidth <= 0)
                return [];
            var truncated = Segment.Truncate(_segment, maxWidth - 1);
            return truncated == null ? [new Segment("…")] : [truncated, new Segment("…")];
        }
    }

    public string Pad(string? text, int length)
    {
        var s = (text ?? string.Empty);
        if (s.Length > length)
            s = s.Substring(0, length - 1) + "…";
        s = s.PadRight(length, ' ');
        return s;
    }

    private static readonly Regex AnsiEscape = new(@"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\)|[@-Z\\-_])", RegexOptions.Compiled);

    /// <summary>
    /// Make text from external sources (process output, logs) safe to measure: Spectre measures cells
    /// differently from the terminal for tabs, control characters and escape sequences, so a line can
    /// render wider than the console, wrap, and scroll the whole live display.
    /// </summary>
    internal static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = AnsiEscape.Replace(text, string.Empty);
        var sb = new StringBuilder(text.Length);
        var column = 0;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                sb.Append(c);
                column = 0;
            }
            else if (c == '\t')
            {
                var spaces = 4 - column % 4;
                sb.Append(' ', spaces);
                column += spaces;
            }
            else if (!char.IsControl(c))
            {
                sb.Append(c);
                column++;
            }
        }
        return sb.ToString();
    }

    private static IRenderable StatusIcon(bool started, bool finished, string spinner)
    {
        var (icon, color) = !started ? ("○", "grey") : !finished ? (spinner, "yellow") : ("✔", "green");
        return new Markup($"[{color}]{icon}[/]");
    }

    public void Dispose()
    {
        _unsubscribe?.Invoke();
        _unsubscribe = null;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }
}
