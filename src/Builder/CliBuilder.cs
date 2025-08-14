using System;
using System.Linq;
using System.Threading;
using CommandDotNet;
using CommandDotNet.Builders.ArgumentDefaults;
using CommandDotNet.Execution;
using CommandDotNet.Help;
using CommandDotNet.IoC.MicrosoftDependencyInjection;
using CommandDotNet.NameCasing;
using CommandDotNet.Spectre;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Terminator.DependencyInjection;
using Terminator.RootCommand;

namespace Terminator.Builder;

public static class CliBuilder
{
    /// <summary>
    /// Initialize a basic new CLI app.
    /// Dependency injection: Any class that implements IServiceCollection in the same assembly as the rootcommand will be loaded
    /// Auto registers the root command and default behaviour, so interactive selection is an option
    /// </summary>
    /// <param name="runner">A Cli app runner</param>
    /// <typeparam name="T">RootCommand class with subcommands defined</typeparam>
    /// <returns></returns>
    public static AppRunner Initialize<T>() where T : class
    {
        var services = new ServiceCollection();
        // add all types where to root command exists
        services.AutoRegisterServiceRegistrars(typeof(T).Assembly);
        var runner = CliBuilder.Configure<T>();
        services.RegisterCommandRunner<T>(runner);
        var provider = services.BuildServiceProvider();
        runner = runner.UseMicrosoftDependencyInjection(provider);
        CtrlCSupport.EnableCtrlC();
        return runner;
    }

    /// <summary>
    /// Configure the CLI app
    /// 
    /// </summary>
    /// <param name="runner">A Cli app runner</param>
    /// <typeparam name="T">RootCommand class with subcommands defined</typeparam>
    /// <returns></returns>
    public static AppRunner Configure<T>(IConfiguration? configuration = null) where T : class
    {
        var runner = new AppRunner<T>().UseNameCasing(Case.KebabCase, true);
        if (configuration != null)
        {
            runner = runner.UseDefaultsFromConfig(DefaultSources.GetValueFunc("Config", key => configuration[key]));
        }

        runner.Configure(b => b.UseMiddleware(RunRootCommandMiddleWare<T>(), MiddlewareStages.ParseInput));
        runner.UseSpectreAnsiConsole().Configure(c =>
        {
            var help = c.AppSettings.Help;
            help.UsageAppNameStyle = UsageAppNameStyle.Adaptive;
            help.TextStyle = HelpTextStyle.Detailed;
            help.PrintHelpOption = true;
            help.ExpandArgumentsInUsage = true;
        });
        return runner;
    }

    /// <summary>
    /// Middleware to run the root command.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private static ExecutionMiddleware RunRootCommandMiddleWare<T>() where T : class
    {
        return async (ctx, next) =>
        {
            if (ctx.ParseResult!.TargetCommand.IsExecutable || ctx.ParseResult.ParseError != null ||
                ctx.ParseResult.HelpWasRequested())
            {
                return await next(ctx);
            }

            var exec = (CommandExecutor<T>)ctx.DependencyResolver!.Resolve(typeof(CommandExecutor<T>))!;
            var console = (IAnsiConsole)ctx.DependencyResolver!.Resolve(typeof(IAnsiConsole))!;
            return await exec.RunCommandAsync(ctx, console);
        };
    }

    /// <summary>
    /// Service registration for the command runners
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="runner"></param>
    public static void RegisterCommandRunner<T>(this IServiceCollection services, AppRunner runner) where T : class
    {
        foreach (var (type, _) in runner.GetCommandClassTypes())
        {
            services.AddTransient(type);
        }

        services.AddTransient<T>();
        services.AddTransient<CommandExecutor<T>>();
        services.AddSingleton((AppRunner<T>)runner);
        services.AddSingleton<IAnsiConsole>(e => AnsiConsole.Console);
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Environment.Exit(0);
        };
        services.AddSingleton(cts);
    }
}