using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class AddExpenseViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;
    private readonly IAppNavigator _navigator;

    public AddExpenseViewModel(FinTrackApiClient api, IAppNavigator navigator)
    {
        _api = api;
        _navigator = navigator;
        Title = "Harcama ekle";
        OccurredOn = DateTime.Today;
    }

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    public ObservableCollection<CardDto> Cards { get; } = [];

    public IReadOnlyList<string> PaymentMethods { get; } =
        ["Cash", "DebitCard", "CreditCard", "BankTransfer", "Other"];

    [ObservableProperty] private string amountText = string.Empty;

    [ObservableProperty] private DateTime occurredOn;

    [ObservableProperty] private CategoryDto? selectedCategory;

    [ObservableProperty] private string paymentMethod = "Cash";

    [ObservableProperty] private CardDto? selectedCard;

    [ObservableProperty] private string description = string.Empty;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var categoriesTask = _api.GetCategoriesAsync(cancellationToken);
            var cardsTask = _api.GetCardsAsync(cancellationToken);
            await Task.WhenAll(categoriesTask, cardsTask);

            Categories.Clear();
            foreach (var category in (await categoriesTask).Where(item => !item.IsArchived))
            {
                Categories.Add(category);
            }

            Cards.Clear();
            foreach (var card in await cardsTask)
            {
                Cards.Add(card);
            }

            SelectedCategory ??= Categories.FirstOrDefault();
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Form yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!Validate(out var request))
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _api.CreateExpenseAsync(request!, cancellationToken);
            await _navigator.GoToAsync("//expenses", cancellationToken);
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Harcama kaydedilemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _navigator.GoBackAsync(cancellationToken);

    private bool Validate(out CreateExpenseRequest? request)
    {
        request = null;
        ErrorMessage = InputValidation.ValidatePositiveAmount(AmountText);
        if (ErrorMessage is not null)
        {
            return false;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = "Kategori seçiniz.";
            return false;
        }

        var occurredOn = DateOnly.FromDateTime(OccurredOn);
        if (occurredOn > DateOnly.FromDateTime(DateTime.Today))
        {
            ErrorMessage = "İleri tarihli harcama girilemez.";
            return false;
        }

        Guid? cardId = null;
        if (string.Equals(PaymentMethod, "CreditCard", StringComparison.Ordinal))
        {
            if (SelectedCard is null)
            {
                ErrorMessage = "Kart seçiniz.";
                return false;
            }

            cardId = SelectedCard.Id;
        }

        if (!InputValidation.TryParseAmount(AmountText, out var amount))
        {
            ErrorMessage = "Geçerli bir tutar giriniz.";
            return false;
        }

        request = new CreateExpenseRequest(
            amount,
            occurredOn,
            SelectedCategory.Id,
            PaymentMethod,
            cardId,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim());
        return true;
    }
}
