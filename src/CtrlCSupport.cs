using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        // Only valid when a real console is attached; throws if stdin is redirected (headless).
        if (!Console.IsInputRedirected)
        {
            try
            {
                Console.TreatControlCAsInput = false;
            }
            catch (Exception)
            {
                // No interactive console available - nothing to configure.
            }
        }

        Console.CancelKeyPress += (sender, e) =>
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                // Second Ctrl+C: user insists. Flush buffered output (important when
                // stdout is redirected/piped) and terminate with the conventional code.
                FlushOutput();
                return; // e.Cancel left false -> runtime terminates the process.
            }

            // First Ctrl+C: keep the process alive and cancel cooperatively so that
            // finally/await-using cleanup runs and buffered output is flushed normally.
            e.Cancel = true;
            CancellationTokenSource.Cancel();
        };

        // Ctrl+C (SIGINT) is covered above. Also react to termination signals sent by
        // orchestrators (kill, docker stop, Kubernetes, systemd, CI cancellation) so a
        // headless process shuts down cooperatively instead of being killed mid-write.
        RegisterPosixSignal(PosixSignal.SIGTERM);
        RegisterPosixSignal(PosixSignal.SIGQUIT);
    }

    private static void RegisterPosixSignal(PosixSignal signal)
    {
        try
        {
            _signalRegistrations.Add(PosixSignalRegistration.Create(signal, context =>
            {
                // Cancel the default OS action (immediate termination) and shut down
                // cooperatively so cleanup runs and buffered output is flushed.
                context.Cancel = true;
                if (!CancellationTokenSource.IsCancellationRequested)
                {
                    CancellationTokenSource.Cancel();
                }
                else
                {
                    // Already cancelling and signalled again: flush and exit promptly.
                    FlushOutput();
                    Environment.Exit(130);
                }
            }));
        }
        catch (Exception)
        {
            // Signal not supported on this platform - safe to ignore.
        }
    }

    private static void FlushOutput()
    {
        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
        }
        catch (Exception)
        {
            // Best effort - nothing more we can do during shutdown.
        }
    }

    // Hold registrations for the process lifetime so they are not garbage collected.
    private static readonly List<PosixSignalRegistration> _signalRegistrations = new();

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