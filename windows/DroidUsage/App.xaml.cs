using DroidUsage.Composition;
using DroidUsage.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace DroidUsage;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private readonly ILogger<App> _logger;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        _services = ServiceRegistration.Build();
        _logger = _services.GetRequiredService<ILogger<App>>();

        UnhandledException += (_, e) =>
            _logger.LogCritical(e.Exception, "Unhandled exception: {Message}", e.Message);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = _services.GetRequiredService<MainWindow>();
            window.Closed += (_, _) => _services.Dispose();
            _window = window;
            _window.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to launch the main window.");
            throw;
        }
    }
}
