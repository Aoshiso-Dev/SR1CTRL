using Microsoft.Extensions.DependencyInjection;
using SR1CTRL.ViewModels;
using SR1CTRL.Views;

namespace SR1CTRL;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
