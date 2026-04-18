using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SR1CTRL.Application;
using SR1CTRL.Infrastructure;
using System.Windows;

namespace SR1CTRL;

public partial class App
{
    private IHost? _host;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddApplication();
                services.AddInfrastructure();
                services.AddPresentation();
                services.AddHostedService<MainWindowHostedService>();
            })
            .Build();

        _host.Start();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (_host is null)
        {
            return;
        }

        await _host.StopAsync(TimeSpan.FromSeconds(2));
        _host.Dispose();
    }
}
