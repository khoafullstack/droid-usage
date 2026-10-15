using System.Net.Http;
using DroidUsage.Configuration;
using DroidUsage.Logging;
using DroidUsage.Platform;
using DroidUsage.Presentation;
using DroidUsage.Services;
using DroidUsage.Services.Abstractions;
using DroidUsage.Services.Api;
using DroidUsage.Services.Auth;
using DroidUsage.Services.Security;
using DroidUsage.Services.Settings;
using DroidUsage.Services.Update;
using DroidUsage.ViewModels;
using DroidUsage.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Composition;

public static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(AppPaths.LogFile));
        });

        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<ISecretStore, WindowsCredentialStore>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IFactoryAuthCrypto, FactoryAuthCrypto>();
        services.AddSingleton<FactoryAuthFileLocator>();
        services.AddSingleton<ICredentialProvider, FactoryCredentialProvider>();
        services.AddSingleton<ITokenRefresher, WorkOsTokenRefresher>();
        services.AddSingleton<IPlanTierProvider, PlanTierProvider>();
        services.AddSingleton<IUsageApiClient, FactoryApiClient>();

        services.AddSingleton<IUpdateService, VelopackUpdateService>();

        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<UsagePresenter>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
