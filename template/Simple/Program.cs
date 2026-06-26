using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Terminator.Builder;
using Terminator.DependencyInjection;

var app = CliBuilder.Initialize<RootCommand>();
try
{
    return await app.RunAsync(args);
}
catch (OperationCanceledException)
{
    // Ctrl+C / SIGTERM cooperative cancellation: exit with the conventional code.
    return 130;
}

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