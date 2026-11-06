using CommandDotNet;
using Spectre.Console;
using Spectre.Console.Testing;
using Terminator.Builder;

namespace Terminator.Tests;

public class CliBuilderTests
{
    [Fact]
    public void Configure_RunsKebabCaseCommandsWithTheGivenConsole()
    {
        var console = new TestConsole();

        var exitCode = CliBuilder.Configure<ConfigureRoot>(null, console).Run("say-hello", "World");

        Assert.Equal(0, exitCode);
        Assert.Contains("Hello World", console.Output);
    }

    [Fact]
    public void Configure_WritesHelpToTheGivenConsole()
    {
        var console = new TestConsole();

        CliBuilder.Configure<ConfigureRoot>(null, console).Run("--help");

        Assert.Contains("say-hello", console.Output);
    }

    [Fact]
    public void Initialize_InjectsTheSameConsoleAsParameterAndService()
    {
        var console = new TestConsole();

        var exitCode = CliBuilder.Initialize<InitializeRoot>(console).Run("check-console");

        Assert.Equal(0, exitCode);
        Assert.Contains("parameter: same", console.Output);
        Assert.Contains("service: same", console.Output);
    }

    [Fact]
    public void Initialize_WithoutArguments_RunsTheCommandPickedInThePrompt()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = CliBuilder.Initialize<PickerRoot>(console).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("Select a command:", console.Output);
        Assert.Contains("ran second", console.Output);
        Assert.DoesNotContain("ran first", console.Output);
    }

    public class ConfigureRoot
    {
        public void SayHello(IAnsiConsole console, [Operand] string name) => console.WriteLine($"Hello {name}");
    }

    public class InitializeRoot(IAnsiConsole injected)
    {
        public void CheckConsole(IAnsiConsole console)
        {
            console.WriteLine($"parameter: {(console is TestConsole ? "same" : "other")}");
            console.WriteLine($"service: {(ReferenceEquals(injected, console) ? "same" : "other")}");
        }
    }

    public class PickerRoot
    {
        [Subcommand] public PickerCommands Commands { get; set; } = new();
    }

    [Command("tools")]
    public class PickerCommands
    {
        public void First(IAnsiConsole console) => console.WriteLine("ran first");

        public void Second(IAnsiConsole console) => console.WriteLine("ran second");
    }
}
