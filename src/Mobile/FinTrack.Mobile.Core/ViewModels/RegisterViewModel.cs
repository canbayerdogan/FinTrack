using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly IAppNavigator _navigator;

    public RegisterViewModel(SessionService session, IAppNavigator navigator)
    {
        _session = session;
        _navigator = navigator;
        Title = "Kayıt ol";
    }

    [ObservableProperty] private string email = string.Empty;

    [ObservableProperty] private string password = string.Empty;

    [ObservableProperty] private string confirmPassword = string.Empty;

    [ObservableProperty] private string preferredTimeZone = "Europe/Istanbul";

    [RelayCommand]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        if (!Validate())
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _session.RegisterAsync(Email.Trim(), Password, PreferredTimeZone.Trim(), cancellationToken);
            await _navigator.GoToAsync("//main", cancellationToken);
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.StatusCode == 409
                ? "Bu e-posta adresi zaten kayıtlı."
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
    private async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _navigator.GoBackAsync(cancellationToken);

    private bool Validate()
    {
        ErrorMessage = InputValidation.ValidateEmail(Email)
                       ?? InputValidation.ValidatePassword(Password)
                       ?? InputValidation.ValidateTimeZone(PreferredTimeZone, required: false);

        if (ErrorMessage is not null)
        {
            return false;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Parolalar eşleşmiyor.";
            return false;
        }

        return true;
    }
}
