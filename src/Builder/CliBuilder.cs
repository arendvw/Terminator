using System;
using System.Threading;
using CommandDotNet;
using CommandDotNet.Builders.ArgumentDefaults;
using CommandDotNet.Execution;
using CommandDotNet.Help;
using CommandDotNet.NameCasing;
using CommandDotNet.Spectre;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Terminator.RootCommand;

namespace Terminator.Builder;

public static class CliBuilder
{
    public static AppRunner Configure<T>(IConfiguration? configuration = null) where T : class
    {
        var runner = new AppRunner<T>().UseNameCasing(Case.KebabCase, true);
        if (configuration != null)
        {
            runner = runner.UseDefaultsFromConfig(DefaultSources.GetValueFunc("Config", key => configuration[key]));
        }
        runner.Configure(b => b.UseMiddleware(RunRootCommandMiddleWare<T>(), MiddlewareStages.ParseInput));
        runner
            .UseSpectreAnsiConsole()
            .UseSpectreArgumentPrompter().Configure(c =>
        {
            var help = c.AppSettings.Help;
            help.UsageAppNameStyle = UsageAppNameStyle.Adaptive;
            help.TextStyle = HelpTextStyle.Detailed;
            help.PrintHelpOption = true;
            help.ExpandArgumentsInUsage = true;
        });
        return runner;
    }

    private static ExecutionMiddleware RunRootCommandMiddleWare<T>() where T : class
    {
        return async (ctx, next) =>
        {
            if (ctx.ParseResult?.TargetCommand.Name != "root-command") return await next(ctx);
            
            var exec = (CommandExecutor<T>)ctx.DependencyResolver!.Resolve(typeof(CommandExecutor<T>))!;
            var console = (IAnsiConsole)ctx.DependencyResolver!.Resolve(typeof(IAnsiConsole))!;
            return await exec.RunCommandAsync(ctx, console);
        };
    }

    public static void RegisterCommandRunner<T>(this IServiceCollection services, AppRunner runner) where T : class
    {
        foreach (var (type,_) in runner.GetCommandClassTypes())
        {
            services.AddTransient(type);
        }

        services.AddSingleton((AppRunner<T>)runner);
        services.AddSingleton<IAnsiConsole>(e => AnsiConsole.Console);
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Ctrl-C detected. Aborting..");
            Environment.Exit(0);
        };
        services.AddSingleton(cts);
    }
}