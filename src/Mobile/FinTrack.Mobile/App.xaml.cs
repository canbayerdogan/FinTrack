using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = _services.GetRequiredService<AppShell>();
        return new Window(shell);
    }

    protected override async void OnStart()
    {
        base.OnStart();

        var session = _services.GetRequiredService<SessionService>();
        var navigator = _services.GetRequiredService<IAppNavigator>();
        var authenticated = await session.IsAuthenticatedAsync();
        await navigator.GoToAsync(authenticated ? "//main" : "//login");
    }
}
