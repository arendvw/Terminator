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

    [Subcommand] public SubCommand SubCommand { get; set; } = new();
}

public class SubCommand
{
    [Command]
    public void MySubCommand(string input)
    {
        Console.WriteLine("Input given: " + input);
    }
}

// add injections here, or remove class
public class MyServiceInjections : IServiceRegistrar
{
    public void RegisterServices(IServiceCollection services)
    {

    }
}