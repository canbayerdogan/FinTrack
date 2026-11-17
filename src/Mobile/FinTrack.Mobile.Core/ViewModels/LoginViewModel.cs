using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly IAppNavigator _navigator;

    public LoginViewModel(SessionService session, IAppNavigator navigator)
    {
        _session = session;
        _navigator = navigator;
        Title = "Giriş";
    }

    [ObservableProperty] private string email = string.Empty;

    [ObservableProperty] private string password = string.Empty;

    [RelayCommand]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (!Validate())
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _session.LoginAsync(Email.Trim(), Password, cancellationToken);
            await _navigator.GoToAsync("//main", cancellationToken);
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.StatusCode == 401
                ? "E-posta veya şifre hatalı."
                : exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "API'ye ulaşılamadı. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("register", cancellationToken);

    private bool Validate()
    {
        var emailError = InputValidation.ValidateEmail(Email);
        if (emailError is not null)
        {
            ErrorMessage = emailError;
            return false;
        }

        var passwordError = InputValidation.ValidateLoginPassword(Password);
        if (passwordError is not null)
        {
            ErrorMessage = passwordError;
            return false;
        }

        ErrorMessage = null;
        return true;
    }
}
