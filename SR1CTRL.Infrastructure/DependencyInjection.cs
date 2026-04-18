using Microsoft.Extensions.DependencyInjection;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Infrastructure.Ports;
using SR1CTRL.Infrastructure.Serial;
using SR1CTRL.Infrastructure.State;

namespace SR1CTRL.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppStateStore, JsonAppStateStore>();
        services.AddSingleton<IComPortProvider, ComPortProvider>();
        services.AddSingleton<ISerialConnectionFactory, SerialConnectionFactory>();

        return services;
    }
}
