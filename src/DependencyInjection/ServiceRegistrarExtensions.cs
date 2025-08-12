using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Terminator.DependencyInjection;

public static class ServiceRegistrarExtensions
{
    /// <summary>
    /// Automatically registers all service registrars in the current assembly.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <remarks>
    /// This method scans the current assembly for types that implement <see cref="IServiceRegistrar.RegisterServices"/>
    /// and invokes their <see cref="IServiceRegistrar"/> method.
    /// </remarks>
    public static void AutoRegisterServiceRegistrars(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(IServiceRegistrar).IsAssignableFrom(type) ||
                !type.IsClass || type.IsAbstract) continue;
            
            var registrar = Activator.CreateInstance(type) as IServiceRegistrar;
            registrar?.RegisterServices(services);
        }
    }
}