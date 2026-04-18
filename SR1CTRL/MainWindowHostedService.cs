using Microsoft.Extensions.Hosting;
using SR1CTRL.ViewModels;
using SR1CTRL.Views;

namespace SR1CTRL;

public sealed class MainWindowHostedService(MainWindow window, MainViewModel viewModel) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        viewModel.SaveCurrentState();
        return Task.CompletedTask;
    }
}
