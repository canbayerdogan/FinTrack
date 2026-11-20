using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class AddExpenseViewModel : ViewModelBase
{
    private const string ExpenseRoute = "/api/v1/expenses";

    private readonly FinTrackApiClient _api;
    private readonly IAppNavigator _navigator;
    private readonly PendingOperationRecoveryService _recovery;
    private readonly SessionService _session;

    public AddExpenseViewModel(
        FinTrackApiClient api,
        IAppNavigator navigator,
        PendingOperationRecoveryService recovery,
        SessionService session)
    {
        _api = api;
        _navigator = navigator;
        _recovery = recovery;
        _session = session;
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
        // IsBusy yalnızca UI kilidi değil; kalıcı pending durumu da aşağıda kontrol edilir.
        if (IsBusy || !Validate(out var request))
        {
            return;
        }

        var userIdKey = await _session.GetUserIdKeyAsync(cancellationToken);
        if (userIdKey is null)
        {
            ErrorMessage = "Oturum bilgisi alınamadı. Lütfen tekrar giriş yapınız.";
            return;
        }

        // Gövde bir kez serileştirilir; pending kaydı ve tüm denemeler aynı baytları kullanır.
        var body = JsonSerializer.SerializeToUtf8Bytes(request!, ApiJson.Options);

        SetBusy(true);
        try
        {
            var existing = await _recovery.FindMatchingAsync(
                userIdKey,
                HttpMethod.Post.Method,
                ExpenseRoute,
                body,
                cancellationToken);

            RecoveryResult result;
            if (existing?.State == PendingOperationState.Uncertain)
            {
                // Belirsiz işlem otomatik olarak yeni harcamaya dönüşmez (docs/11 §7.5).
                ErrorMessage = PendingOperationRecoveryService.UnknownResultMessage;
                return;
            }

            if (existing is null)
            {
                var operation = await _recovery.CreateAsync(
                    userIdKey,
                    HttpMethod.Post.Method,
                    ExpenseRoute,
                    body,
                    cancellationToken);
                result = await _recovery.SendAsync(operation.Key, cancellationToken);
            }
            else if (existing.State == PendingOperationState.Created)
            {
                result = await _recovery.SendAsync(existing.Key, cancellationToken);
            }
            else
            {
                result = await _recovery.RecoverOneAsync(existing.Key, cancellationToken);
            }

            if (result.Status == RecoveryStatus.Completed)
            {
                await _navigator.GoToAsync("//expenses", cancellationToken);
                return;
            }

            ErrorMessage = result.Status == RecoveryStatus.Skipped
                ? "İşlem tamamlandı; kayıtlarını kontrol edebilirsin."
                : result.Message;
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
