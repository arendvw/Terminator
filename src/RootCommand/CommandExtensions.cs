using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandDotNet;
using Spectre.Console;
using Terminator.DependencyInjection;

namespace Terminator.RootCommand;

public static class CommandExtensions
{
    public static IEnumerable<CommandDescriptor> GetCommands(this Command command, CommandDescriptor? parent = null)
    {
        var cmd = new CommandDescriptor()
        {
            Description = command.Description ?? "",
            CommandPath =
            [
                ..parent?.CommandPath ?? [],
                command.Name
            ]
        };
        
        if (command.IsExecutable && command.Name != "root-command")
        {
            yield return cmd;
        }
        foreach (var item in command.Subcommands)
        {
            foreach (var subItem in GetCommands(item, cmd))
            {
                yield return subItem;
            }
        }
    }
    
    public static async Task<int> RunCommandAsync<T>(this CommandExecutor<T> executor, CommandContext context, IAnsiConsole ansiConsole) where T : class
    {
        var commands = context.RootCommand!
            .Subcommands.SelectMany(s => GetCommands(s));

        var prompt = new SelectionPrompt<CommandDescriptor>()
            .Title("Select a command:")
            .AddChoices(commands)
            .SearchPlaceholderText("Search...");
        
        prompt.SearchEnabled = true;
        var choice = prompt.ShowWithCancel(ansiConsole);

        // Run the selected command with full parsing
        return await executor.RunCommandAsync(string.Join(" ",choice.Command));
    }
}