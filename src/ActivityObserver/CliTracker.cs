using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Terminator.ActivityObserver; 
/// <summary>
/// A tracker for CLI activities that observes and displays activity progress, logs, and other related information.
/// </summary>
public sealed class CliTracker(IAnsiConsole console) : IAsyncDisposable
{
    public ActivityObservationTable Table { get; } = new();
    private readonly LiveActivityRenderer _live = new(console);
    private readonly CancellationTokenSource _cts = new();
    private Task? _uiTask;

    /// <summary>
    /// Use the whole terminal window while the live display runs, when the console is an interactive
    /// ANSI terminal; the final state is written to the normal screen when it stops. Defaults to
    /// <c>true</c>. Redirected output and consoles without ANSI support always get the inline display.
    /// </summary>
    public bool FullScreen { get; init; } = true;

    public void Show()
    {
        _live.FullScreen = FullScreen;
        _uiTask = _live.RunAsync(Table, _cts.Token);
    }
    
    public async Task Stop()
    {
        await _cts.CancelAsync();
        if (_uiTask is not null)
        {
            await _uiTask.ConfigureAwait(false);
        }

    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            if (_uiTask is not null)
            {
                await _uiTask.ConfigureAwait(false);
            }
        } catch { /* ignore */ }
        _cts.Dispose();
        _live.Dispose();
    }
}