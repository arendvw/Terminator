// See https://aka.ms/new-console-template for more information

using System.ComponentModel;
using System.Windows.Input;
using AutoSpectre;
using AutoSpectre.Extensions;
using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Terminator.Builder;
using Terminator.DependencyInjection;

var app = CliBuilder.Initialize<ExampleApp.RootCommand>();
await app.RunAsync(args);

namespace ExampleApp
{
    public class RootCommand
    {
        [Command]
        public void TestCommand(Example form, IAnsiConsole console, ExampleSpectreFactory factory)
        {
            console.WriteLine("Hello, World!");
            form.Prompt();
        }

        [Subcommand] public SubCommand SubCommand { get; set; }
    }

    public enum SomeEnum
    {
        OptionA,
        OptionB
    }
    
    [AutoSpectreForm(Culture = "da-DK")]
    public class Example : IArgumentModel
    {
        [Option]
        [TextPrompt(Title = "Add item value")]
        public int[] IntItems { get; set; } = [];

        [Option]
        [TextPrompt(Title = "Enter first name", DefaultValueStyle = "bold")]
        public string? FirstName { get; set; }
        
        [Option]
        [TextPrompt(PromptStyle = "green bold")]
        public bool LeftHanded { get; set; }

        [Option]
        [TextPrompt(Title = "Choose your [red]value[/]")]
        public SomeEnum Other { get; set; }

        [Option]
        [TextPrompt(Secret = true, Mask = '*')]
        public string? Password { get; set; }
    }
    public class Test {
        public string? FirstName { get; set; }

        public readonly string? FirstNameDefaultValue = "John Doe";
        
        public static readonly string[] NameChoices = new[] { "Kurt", "Krist", "David", "Pat" };
        
        [TaskStep(UseStatus = true, StatusText = "This will take a while", SpinnerType = SpinnerKnownTypes.Christmas,
            SpinnerStyle = "green on yellow")]
        public void DoSomething(IAnsiConsole console)
        {
            console.Write(new FigletText("A figlet text is needed"));
        }

        [Option]
        [SelectPrompt(Source = nameof(ItemSource))]
        public string Item { get; set; } = string.Empty;

        [Option]
        public List<string> ItemSource { get; } = new() { "Alpha", "Bravo", "Charlie" };

        [Option]
        [SelectPrompt(InstructionsText = "Check the special items you want to select")]
        //[SelectPrompt(Converter = nameof(SpecialProjectionConverter))]
        public List<int> SpecialProjection { get; set; } = new();

        
        public string SpecialProjectionConverter(int source) => $"Number {source}";
        public List<int> SpecialProjectionSource { get; set; } = new() { 1, 2, 3, 4 };

        [TextPrompt]
        [Option]
        // [TextPrompt(Validator = nameof(EnterYearValidator))]
        public int EnterYear { get; set; }

        public string? EnterYearValidator(int year)
        {
            return year <= DateTime.Now.Year ? null : "Year cannot be larger than current year";
        }

        [Option]
        [TextPrompt] public HashSet<string> Names { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? NamesValidator(List<string> items, string newItem)
        {
            if (newItem == "Foobar")
                return "Cannot be Foobar";

            if (items.Contains(newItem))
                return $"{newItem} has already been added";

            return null;
        }

        
        [TextPrompt] public bool AddExistingName { get; set; }

        [Option]
        [TextPrompt(Condition = nameof(AddExistingName))]
        public string? ExistingName { get; set; }

        [Option]
        [TextPrompt(Condition = nameof(AddExistingName), NegateCondition = true)]
        public string? NewName { get; set; }

        [Option]
        [SelectPrompt(Source = nameof(SearchStringSource), SearchEnabled = true,
            SearchPlaceholderText = "Placeholder \"text\"")]
        public string SearchString { get; set; }

        public string[] SearchStringSource() => new[] { "First", "Second" };
    }

    public class SubCommand
    {
        [Command]
        public async Task MySubCommand(string input)
        {
            Console.WriteLine("Input given");
        }
    }

    public class MyServiceInjections() : IServiceRegistrar
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<ExampleSpectreFactory>();
            AnsiConsole.WriteLine("Add your own injections here.");
        }
    }
}