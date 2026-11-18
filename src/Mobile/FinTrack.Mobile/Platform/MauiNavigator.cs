using FinTrack.Mobile.Core.Abstractions;
using Microsoft.Maui.Controls;

namespace FinTrack.Mobile.Platform;

/// <summary>
/// Shell tabanlı gezinme. Giriş/kayıt ekranlarında flyout kapatılır;
/// oturum açıldıktan sonra ana flyout yeniden etkinleştirilir.
/// </summary>
public sealed class MauiNavigator : IAppNavigator
{
    public Task GoToAsync(string route, CancellationToken cancellationToken = default)
    {
        if (Shell.Current is null)
        {
            return Task.CompletedTask;
        }

        var isAuthScreen = route.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                           route.Contains("register", StringComparison.OrdinalIgnoreCase);
        Shell.Current.FlyoutBehavior = isAuthScreen ? FlyoutBehavior.Disabled : FlyoutBehavior.Flyout;
        return Shell.Current.GoToAsync(route);
    }

    public Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (Shell.Current is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.Navigation.NavigationStack.Count > 1
            ? Shell.Current.GoToAsync("..")
            : Shell.Current.GoToAsync("//main");
    }

    public Task DisplayAlertAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.DisplayAlertAsync(title, message, "Tamam") ?? Task.CompletedTask;
    }
}
