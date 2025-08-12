using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Terminator.RootCommand;

/// <summary>
/// This is the base command that executes all defined commands
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="appRunner"></param>
/// <param name="serviceProvider"></param>
public class CommandExecutor<T>(AppRunner<T> appRunner, IServiceProvider serviceProvider) where T : class
{
    // Get all command instances from DI
    public IEnumerable<object> GetAllCommands()
    {
        return appRunner.GetCommandClassTypes()
            .Select(x => x.type)
            .Select(serviceProvider.GetRequiredService);
    }

    // Run command with full CommandDotNet parsing pipeline
    public async Task<int> RunCommandAsync(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return await appRunner.RunAsync(args);
    }

    // Run command with args array
    public async Task<int> RunCommandAsync(params string[] args)
    {
        return await appRunner.RunAsync(args);
    }

    private IEnumerable<MethodInfo> GetCommandMethods(Type type)
    {
        return type.GetMethods()
            .Where(m => m.GetCustomAttribute<CommandAttribute>() != null);
    }
}