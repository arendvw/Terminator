using System;
using Spectre.Console;

namespace Terminator.ActivityObserver;

/// <summary>
/// Extension methods for logging activity events to an IAnsiConsole.
/// </summary>
public static class ActivityConsoleLoggerExtensions
{
    /// <summary>
    /// Logs a step start event to the console.
    /// </summary>
    public static void LogStepStarted(this IAnsiConsole console, string description)
    {
        console.MarkupLineInterpolated($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [yellow]▶[/] Starting: {description}");
    }

    /// <summary>
    /// Logs a step completion event to the console.
    /// </summary>
    public static void LogStepCompleted(this IAnsiConsole console, string description, double durationSeconds)
    {
        console.MarkupLineInterpolated($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [green]✓[/] Completed: {description} [grey]({durationSeconds:0.0}s)[/]");
    }

    /// <summary>
    /// Logs a progress update to the console.
    /// </summary>
    public static void LogProgress(this IAnsiConsole console, string description, double progress, string? message = null)
    {
        if (progress <= 0) return;
        
        var progressText = $"{progress:P0}";
        var progressInfo = string.IsNullOrEmpty(message) 
            ? progressText 
            : $"{progressText} - {message}";
            
        console.MarkupLineInterpolated($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [blue]↻[/] Progress: {description} [yellow]{progressInfo}[/]");
    }

    /// <summary>
    /// Logs a message with the specified log level to the console.
    /// </summary>
    public static void LogMessage(this IAnsiConsole console, string description, CliLogLevel level, string message)
    {
        var color = ColorFor(level);
        console.MarkupLineInterpolated($"[grey][[{DateTime.Now:HH:mm:ss}]][/] [{color}]{LevelIcon(level)}[/] {description}: [{color}]{message}[/]");
    }

    /// <summary>
    /// Gets the appropriate icon for a log level.
    /// </summary>
    private static string LevelIcon(CliLogLevel level) => level switch
    {
        CliLogLevel.Debug => "…",
        CliLogLevel.Information => "ℹ",
        CliLogLevel.Success => "✓",
        CliLogLevel.Warning => "⚠",
        CliLogLevel.Error => "✗",
        _ => "•"
    };

    /// <summary>
    /// Gets the appropriate color for a log level.
    /// </summary>
    private static string ColorFor(CliLogLevel level) => level switch
    {
        CliLogLevel.Debug => "grey",
        CliLogLevel.Information => "white",
        CliLogLevel.Success => "green",
        CliLogLevel.Warning => "yellow",
        CliLogLevel.Error => "red",
        _ => "white"
    };
}
