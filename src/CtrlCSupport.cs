using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Terminator;

/// <summary>
/// By default, Spectre.Console does not support ctrl C
/// This is a helper class to enable ctrl C support
/// </summary>
public static class CtrlCSupport
{
    public static Task<T> ShowWithCancelAsync<T>(this IPrompt<T> prompt, IAnsiConsole console)
    {
        return prompt.ShowAsync(console, CancellationTokenSource.Token);
    }

    public static void EnableCtrlC()
    {
        Console.TreatControlCAsInput = false;
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent the process from terminating
            CancellationTokenSource.Cancel();
            Environment.Exit(0); // Exit the application gracefully
        };
    }

    /// <summary>
    /// Call this with
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="prompt"></param>
    /// <param name="console"></param>
    /// <returns></returns>
    public static T ShowWithCancel<T>(this IPrompt<T> prompt, IAnsiConsole console)
    {
        return prompt.ShowWithCancelAsync(console).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

}