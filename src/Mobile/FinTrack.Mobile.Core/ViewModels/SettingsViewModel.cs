using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;
    private readonly SessionService _session;
    private readonly IAppNavigator _navigator;

    public SettingsViewModel(FinTrackApiClient api, SessionService session, IAppNavigator navigator)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        Title = "Ayarlar";
    }

    [ObservableProperty] private string email = string.Empty;

    [ObservableProperty] private string preferredTimeZone = "Europe/Istanbul";

    [ObservableProperty] private string rowVersion = string.Empty;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var profile = await _api.GetProfileAsync(cancellationToken);
            Email = profile.Email;
            PreferredTimeZone = profile.PreferredTimeZone;
            RowVersion = profile.RowVersion;
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Profil yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = InputValidation.ValidateTimeZone(PreferredTimeZone);
        if (ErrorMessage is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RowVersion))
        {
            ErrorMessage = "Profil sürümü alınamadı. Lütfen ekranı yenileyin.";
            return;
        }

        SetBusy(true);
        try
        {
            var profile = await _api.UpdateProfileAsync(PreferredTimeZone.Trim(), RowVersion, cancellationToken);
            PreferredTimeZone = profile.PreferredTimeZone;
            RowVersion = profile.RowVersion;
            await _navigator.DisplayAlertAsync("Ayarlar", "Saat dilimi kaydedildi.", cancellationToken);
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.StatusCode == 409
                ? "Profil başka bir cihazda değiştirildi. Lütfen yenileyin."
                : exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Kaydedilemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            await _session.LogoutAsync(cancellationToken);
            await _navigator.GoToAsync("login", cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
