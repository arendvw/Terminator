using System;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Terminator;

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
    
    public static T ShowWithCancel<T>(this IPrompt<T> prompt, IAnsiConsole console)
    {
        return prompt.ShowWithCancelAsync(console).GetAwaiter().GetResult();
    }

    public static CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

}