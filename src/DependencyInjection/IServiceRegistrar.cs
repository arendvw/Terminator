using Microsoft.Extensions.DependencyInjection;

namespace Terminator.DependencyInjection;

public interface IServiceRegistrar
{
    void RegisterServices(IServiceCollection services);
}