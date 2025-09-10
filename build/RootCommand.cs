using BuildTools.Build;
using CommandDotNet;
using JetBrains.Annotations;
using Spectre.Console;
using Terminator.RootCommand;

// ReSharper disable UnusedMember.Global

namespace BuildTools;
/// <summary>
/// Reference all rootcommands here 
/// </summary>
[UsedImplicitly]
public class RootCommand(CommandExecutor<RootCommand> executor, IAnsiConsole ansiConsole) 
{
    [Subcommand(RenameAs = "build")]
    public BuildCommands? Build { get; set; }
}
