using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.ViewModels;
using FinTrack.Mobile.Platform;
using FinTrack.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace FinTrack.Mobile;

public static class MauiProgram
{
    private const string DefaultBaseUrl =
#if ANDROID
        "https://10.0.2.2:7241/api/v1/";
#else
        "https://localhost:7241/api/v1/";
#endif

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var baseUrl = Preferences.Get("api_base_url", DefaultBaseUrl);
        var apiAddress = new Uri(baseUrl);

        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<ISecureKeyValueStore, SecureStorageKeyValueStore>();
        builder.Services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
        builder.Services.AddSingleton<IPendingOperationStore>(services =>
            new PendingOperationStore(
                Path.Combine(FileSystem.AppDataDirectory, "fintrack-pending-operations.db"),
                services.GetRequiredService<ISecureKeyValueStore>(),
                services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<IAppNavigator, MauiNavigator>();

        builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = apiAddress });
        builder.Services.AddSingleton<SessionRefresher>(services =>
            new SessionRefresher(
                services.GetRequiredService<HttpClient>(),
                services.GetRequiredService<ITokenStore>()));
        builder.Services.AddSingleton<AuthHandler>(services =>
            new AuthHandler(
                services.GetRequiredService<ITokenStore>(),
                services.GetRequiredService<SessionRefresher>(),
                services.GetRequiredService<IPendingOperationStore>())
            {
                InnerHandler = new HttpClientHandler(),
            });
        builder.Services.AddSingleton(services =>
            new FinTrackApiClient(new HttpClient(services.GetRequiredService<AuthHandler>())
            {
                BaseAddress = apiAddress,
            }));
        builder.Services.AddSingleton(services =>
            new PendingOperationRecoveryService(
                new HttpClient(services.GetRequiredService<AuthHandler>())
                {
                    BaseAddress = apiAddress,
                },
                services.GetRequiredService<IPendingOperationStore>(),
                services.GetRequiredService<TimeProvider>(),
                currentUserKeyProvider: services.GetRequiredService<SessionService>().GetUserIdKeyAsync));

        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<PendingOperationStartup>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ExpensesViewModel>();
        builder.Services.AddTransient<AddExpenseViewModel>();
        builder.Services.AddTransient<CardsViewModel>();
        builder.Services.AddTransient<CardDetailViewModel>();
        builder.Services.AddTransient<DebtsViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ExpensesPage>();
        builder.Services.AddTransient<AddExpensePage>();
        builder.Services.AddTransient<CardsPage>();
        builder.Services.AddTransient<CardDetailPage>();
        builder.Services.AddTransient<DebtsPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<SettingsPage>();

        var app = builder.Build();
        var authHandler = app.Services.GetRequiredService<AuthHandler>();
        authHandler.SessionExpired += async () =>
            await app.Services.GetRequiredService<IAppNavigator>().GoToAsync("login");
        return app;
    }
}
