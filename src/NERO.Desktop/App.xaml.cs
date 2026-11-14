using System.Threading;
using System.Windows;
using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NERO.AI;
using NERO.Core.Settings;
using NERO.Security;

namespace NERO.Desktop;

public partial class App : WpfApplication
{
    private const string MutexName = @"Local\NERO.Desktop.SingleInstance";
    private Mutex? _mutex;
    private IHost? _host;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var firstInstance);
        if (!firstInstance) { Shutdown(); return; }

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<JsonNeroUserSettingsStore>();
                services.AddSingleton<IDeviceSecretStore, WindowsCredentialManagerSecretStore>();
                services.AddHttpClient<IGeminiApiClient, GeminiApiClient>(client =>
                    client.Timeout = TimeSpan.FromSeconds(45));
                services.AddSingleton<GeminiDeviceConfiguration>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (MainWindow is NERO.Desktop.MainWindow neroWindow) neroWindow.DisposeTrayIcon();
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* Startup may have ended before ownership was acquired. */ }
            _mutex.Dispose();
        }
    }
}
