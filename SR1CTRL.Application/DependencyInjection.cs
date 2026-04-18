using Microsoft.Extensions.DependencyInjection;
using SR1CTRL.Application.UseCases;

namespace SR1CTRL.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceController, DeviceController>();
        services.AddSingleton<DeviceControlUseCase>();

        return services;
    }
}
