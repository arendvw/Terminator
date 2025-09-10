using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Terminator.DependencyInjection;
using Terminator.RootCommand;

namespace BuildTools.Bootstrap;

public class Services : IServiceRegistrar
{
    public void RegisterServices(IServiceCollection services)
    {
        //services.AddSingleton<ActivitySource>(e => new ActivitySource("CommandLine"));
    }
}