using System;
using System.Linq;
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

    private Layout Build(ActivityObservationTable table, int frameCount = 0)
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
            .Select(e => Markup.FromInterpolated($"{e.TimeUtc:hh:mm:ss}: {e.Message}"));

        var logs = new Grid().AddColumn();
        foreach (var item in messages)
        {
            logs.AddRow([item]);
        }

        layout["Log"].Update(new Panel(logs).Expand());

        foreach (var e in table.SnapshotOrderedByStartUtc())
        {
            var progress = e.LastProgress.HasValue
                ? $"{e.LastProgress:P0}"
                : "";

            var duration = (e?.EndedUtc - e?.StartedUtc)?.TotalSeconds;
            t.AddRow(
                StatusIcon(e?.StartedUtc != null, e?.EndedUtc != null, frame),
                new Markup(e?.Description ?? ""),
                new Markup(Pad(e?.LastProgressMessage, 50)),
                new Markup(e?.EndedUtc == null ? progress : $"{duration:0.0}s")
            );
        }

        layout["Status"].Update(new Panel(t).Expand());

        return layout;
    }

    public string Pad(string? text, int length)
    {
        var s = (text ?? string.Empty);
        if (s.Length > length)
            s = s.Substring(0, length - 2) + ..;
        s = s.PadRight(length, ' ');
        return s;
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
