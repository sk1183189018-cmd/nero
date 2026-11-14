using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NERO.Desktop;

public partial class App : Application
{
    private IHost? _host;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(services => services.AddSingleton<MainWindow>())
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
    }
}
