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
/// Render a live updatable progress of each activity step
/// </summary>
public sealed class LiveActivityRenderer : IDisposable
{
    private readonly IAnsiConsole _console;
    private Action? _unsubscribe;
    private readonly Spinner _spinner = Spinner.Known.Default;
    private Timer? _timer;
    private event Action<object>? Refresh;

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

    public async Task RunAsync(ActivityObservationTable table, CancellationToken token)
    {
        // initial target
        var target = Build(table);

        int frames = 0;
        await _console.Live(target)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // subscribe to updates; on any event, rebuild + refresh
                _unsubscribe = Subscribe(table, () =>
                {
                    frames++;
                    ctx.UpdateTarget(Build(table, frames));
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
                    ctx.UpdateTarget(Build(table, frames));
                    ctx.Refresh();
                    _unsubscribe?.Invoke();
                    _unsubscribe = null;
                }
            });
    }

    public bool HasChange { get; set; }

    private Action Subscribe(ActivityObservationTable table, Action onChanged)
    {
        //table.Update += Update;
        Refresh += Update;
        return () =>
        {
            //table.Update -= Update;
            Refresh -= Update;
        };

        void Update(object _) => onChanged();
    }

    private string SpinnerFrame(int frameCount)
    {
        var frame = frameCount % _spinner.Frames.Count;
        return $"{_spinner.Frames[frame]}";
    }

    private IRenderable Build(ActivityObservationTable table, int frameCount = 0)
    {
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Status"),
                new Layout("Log"));
        var frame = SpinnerFrame(frameCount);
        var t = new Table()
            .Border(TableBorder.Simple)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("")
            .AddColumn("")
            .AddColumn("");
        var messages = table.LatestLogs
            .Take(20)
            .Reverse()
            .Select(e => Markup.FromInterpolated($"{e.TimeUtc:hh:mm:ss}: {Sanitize(e.Message)}"));

        var logs = new Grid().AddColumn();
        foreach (var item in messages)
        {
            logs.AddRow([item]);
        }

        var logPanel = new Panel(logs).Expand();
        layout["Log"].Update(logPanel);

        foreach (var e in table.SnapshotOrderedByStartUtc())
        {
            var progress = e.LastProgress.HasValue
                ? $"{e.LastProgress:P0}"
                : "";

            var duration = (e?.EndedUtc - e?.StartedUtc)?.TotalSeconds;
            t.AddRow(
                StatusIcon(e?.StartedUtc != null, e?.EndedUtc != null, frame),
                new Markup(e?.Description ?? ""),
                new Markup(Markup.Escape(Pad(Sanitize(e?.LastProgressMessage), 50))),
                new Markup(e?.EndedUtc == null ? progress : $"{duration:0.0}s")
            );
        }

        var statusPanel = new Panel(t).Expand();
        layout["Status"].Update(statusPanel);

        return new ContentHeightLayout(layout, statusPanel, logPanel);
    }

    /// <summary>
    /// A Layout always fills the whole console height. In a live display that leaves no room for
    /// anything written to the console meanwhile, so every write scrolls the screen and the display jumps.
    /// Render the layout only as tall as its tallest column, leaving at least one free line.
    /// </summary>
    private sealed class ContentHeightLayout(Layout layout, params IRenderable[] columns) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth) => ((IRenderable)layout).Measure(options, maxWidth);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var columnWidth = Math.Max(1, maxWidth / columns.Length);
            var contentHeight = columns
                .Select(c => Segment.SplitLines(c.Render(options, columnWidth)).Count)
                .DefaultIfEmpty(1)
                .Max();
            var height = Math.Max(1, Math.Min(contentHeight, options.ConsoleSize.Height - 1));
            return ((IRenderable)layout).Render(options with { Height = height }, maxWidth);
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
