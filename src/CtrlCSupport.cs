using System;
using System.Threading;
using System.Threading.Tasks;
using CommandDotNet.Prompts;
using Spectre.Console;

namespace Terminator;

/// <summary>
/// By default, Spectre.Console does not support ctrl C
/// This is a helper class to enable ctrl C support
/// </summary>
public static class CtrlCSupport
{


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

    public static Task<T> ShowWithCancelAsync<T>(this IPrompt<T> prompt, IAnsiConsole console)
    {
        return prompt.ShowAsync(console, CancellationTokenSource.Token);
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
    public static T? AskWithCancel<T>(this IAnsiConsole console, string text)
    {
        var prompt = new TextPrompt<T>(text);
        return prompt.ShowWithCancel(console);
    }
    public static async Task<T?> AskWithCancelAsync<T>(this IAnsiConsole console, string text)
    {
        var prompt = new TextPrompt<T>(text);
        return await prompt.ShowWithCancelAsync(console);
    }

    public static T? PromptWithCancel<T>(this IAnsiConsole console, TextPrompt<T> text)
    {
        return text.ShowWithCancel(console);
    }
    public static async Task<T?> PromptWithCancelAsync<T>(this IAnsiConsole console, TextPrompt<T> text)
    {
        return await text.ShowWithCancelAsync(console);
    }
    public static CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

}