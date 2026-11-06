using System;
using System.IO;
using System.Text;
using System.Threading;
using CommandDotNet;
using CommandDotNet.Rendering;
using Spectre.Console;

namespace Terminator.Builder;

/// <summary>
/// Connects CommandDotNet to Spectre.Console. This replaces <c>UseSpectreAnsiConsole</c> from
/// CommandDotNet.Spectre, which is compiled against the unsigned Spectre.Console 0.49 and so pins every
/// consumer to it: from 0.50 on Spectre.Console is strong-named, a different assembly identity.
/// </summary>
internal static class AnsiConsoleBridge
{
    /// <summary>
    /// Forward CommandDotNet's console output (help, errors) to <paramref name="ansiConsole"/> and make
    /// <see cref="IAnsiConsole"/> available as a command parameter.
    /// </summary>
    public static AppRunner UseAnsiConsole(this AppRunner appRunner, IAnsiConsole? ansiConsole = null)
    {
        return appRunner.Configure(c =>
        {
            ansiConsole ??= AnsiConsole.Console;
            c.Console = new AnsiConsoleForwardingConsole(ansiConsole);
            c.UseParameterResolver(_ => ansiConsole);
            c.Services.Add(ansiConsole);
        });
    }

    /// <summary>
    /// A CommandDotNet console that writes through an <see cref="IAnsiConsole"/>.
    /// </summary>
    internal sealed class AnsiConsoleForwardingConsole : SystemConsole
    {
        private readonly IAnsiConsole _ansiConsole;

        public AnsiConsoleForwardingConsole(IAnsiConsole ansiConsole)
        {
            _ansiConsole = ansiConsole;
            Out = new ForwardingTextWriter(text => ansiConsole.Write(text ?? string.Empty));
        }

        public override TextWriter Out { get; }

        public override Encoding OutputEncoding
        {
            get => _ansiConsole.Profile.Encoding;
            set => _ansiConsole.Profile.Encoding = value;
        }

        public override int WindowWidth
        {
            get => _ansiConsole.Profile.Width;
            set => _ansiConsole.Profile.Width = value;
        }

        public override int WindowHeight
        {
            get => _ansiConsole.Profile.Height;
            set => _ansiConsole.Profile.Height = value;
        }

        public override ConsoleKeyInfo? ReadKey(bool intercept = false) =>
            _ansiConsole.Input.ReadKeyAsync(intercept, CancellationToken.None).GetAwaiter().GetResult();
    }
}
