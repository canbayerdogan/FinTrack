using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class CardsViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;
    private readonly IAppNavigator _navigator;

    public CardsViewModel(FinTrackApiClient api, IAppNavigator navigator)
    {
        _api = api;
        _navigator = navigator;
        Title = "Kartlar";
    }

    public ObservableCollection<CardListItem> Items { get; } = [];

    [ObservableProperty] private string summary = "Aktif kart yok.";

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var cards = await _api.GetCardsAsync(cancellationToken);
            Items.Clear();
            foreach (var card in cards)
            {
                Items.Add(new CardListItem(
                    card.Id.ToString("D"),
                    card.DisplayName,
                    card.BankName,
                    card.CreditLimit.HasValue ? MoneyFormat.Format(card.CreditLimit.Value) : "Limit yok",
                    card.Status));
            }

            Summary = $"{cards.Count} aktif kart";
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Kartlar yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenCardAsync(CardListItem item, CancellationToken cancellationToken) =>
        await _navigator.GoToAsync($"carddetail?id={item.Id}", cancellationToken);

    [RelayCommand]
    private async Task AddCardAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("addcard", cancellationToken);

    [RelayCommand]
    private async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("//main", cancellationToken);
}
