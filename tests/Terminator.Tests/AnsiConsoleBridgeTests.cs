using CommandDotNet;
using Spectre.Console;
using Terminator.Builder;

namespace Terminator.Tests;

public class AnsiConsoleBridgeTests
{
    [Fact]
    public void Commands_ReceiveTheAnsiConsoleAsParameter()
    {
        var (console, output) = CreateConsole();

        var exitCode = new AppRunner<App>().UseAnsiConsole(console).Run("Greet", "World");

        Assert.Equal(0, exitCode);
        Assert.Contains("Hello World", output.ToString());
    }

    [Fact]
    public void CommandDotNetOutput_IsForwardedToTheAnsiConsole()
    {
        var (console, output) = CreateConsole();

        new AppRunner<App>().UseAnsiConsole(console).Run("Echo", "forwarded");

        Assert.Contains("forwarded", output.ToString());
    }

    [Fact]
    public void Help_IsWrittenToTheAnsiConsole()
    {
        var (console, output) = CreateConsole();

        new AppRunner<App>().UseAnsiConsole(console).Run("--help");

        Assert.Contains("Greet", output.ToString());
    }

    [Fact]
    public void ConsoleDimensions_ComeFromTheAnsiConsole()
    {
        var (console, _) = CreateConsole();
        var forwarding = new AnsiConsoleBridge.AnsiConsoleForwardingConsole(console);

        Assert.Equal(97, forwarding.WindowWidth);
        Assert.Equal(31, forwarding.WindowHeight);
    }

    private static (IAnsiConsole Console, StringWriter Output) CreateConsole()
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(output),
        });
        console.Profile.Width = 97;
        console.Profile.Height = 31;
        return (console, output);
    }

    public class App
    {
        public void Greet(IAnsiConsole console, [Operand] string name) => console.WriteLine($"Hello {name}");

        public void Echo(IConsole console, [Operand] string text) => console.Out.WriteLine(text);
    }
}
