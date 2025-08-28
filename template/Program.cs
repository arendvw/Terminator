// See https://aka.ms/new-console-template for more information

using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Terminator.Builder;
using Terminator.DependencyInjection;

var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);
    public class RootCommand
    {
        [Command]
        public void TestCommand(IAnsiConsole console)
        {
            console.WriteLine("Hello, World!");
        }

        [Subcommand] public SubCommand SubCommand { get; set; }
    }

    public class SubCommand
    {
        [Command]
        public async Task MySubCommand(string input)
        {
            Console.WriteLine("Input given" + input);
        }
    }

    public class MyServiceInjections() : IServiceRegistrar
    {
        public void RegisterServices(IServiceCollection services)
        {
            AnsiConsole.WriteLine("Add your own injections here.");
        }
    }