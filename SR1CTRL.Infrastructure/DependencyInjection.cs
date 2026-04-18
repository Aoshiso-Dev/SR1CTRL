using Microsoft.Extensions.DependencyInjection;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Infrastructure.Ports;
using SR1CTRL.Infrastructure.Serial;

namespace SR1CTRL.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IComPortProvider, ComPortProvider>();
        services.AddSingleton<ISerialConnectionFactory, SerialConnectionFactory>();

        return services;
    }
}
