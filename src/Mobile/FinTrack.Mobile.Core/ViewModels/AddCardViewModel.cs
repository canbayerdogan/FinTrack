using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.Validation;

namespace FinTrack.Mobile.Core.ViewModels;

/// <summary>
/// Yeni kart tanımı oluşturur. Gerçek kart bilgisi istenmez; yalnızca banka ve
/// kullanıcıya görünen kart adı zorunludur (docs/05 §5.1). POST /cards
/// idempotency kapsamında olduğu için gönderim pending-operation katmanından yapılır.
/// </summary>
public partial class AddCardViewModel : ViewModelBase
{
    private const string CardRoute = "/api/v1/cards";
    private const int NameMaxLength = 100;
    private const int MinDay = 1;
    private const int MaxDay = 31;
    private const int CreditLimitScale = 4;
    private const decimal MaxCreditLimit = 999_999_999_999_999.9999m;

    private readonly IAppNavigator _navigator;
    private readonly PendingOperationRecoveryService _recovery;
    private readonly SessionService _session;

    public AddCardViewModel(
        IAppNavigator navigator,
        PendingOperationRecoveryService recovery,
        SessionService session)
    {
        _navigator = navigator;
        _recovery = recovery;
        _session = session;
        Title = "Kart ekle";
    }

    [ObservableProperty] private string bankName = string.Empty;

    [ObservableProperty] private string displayName = string.Empty;

    [ObservableProperty] private string creditLimitText = string.Empty;

    [ObservableProperty] private string statementDayText = string.Empty;

    [ObservableProperty] private string paymentDueDayText = string.Empty;

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
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
                CardRoute,
                body,
                cancellationToken);

            RecoveryResult result;
            if (existing?.State == PendingOperationState.Uncertain)
            {
                ErrorMessage = PendingOperationRecoveryService.UnknownResultMessage;
                return;
            }

            if (existing is null)
            {
                var operation = await _recovery.CreateAsync(
                    userIdKey,
                    HttpMethod.Post.Method,
                    CardRoute,
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
                await _navigator.GoToAsync("//cards", cancellationToken);
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
            ErrorMessage = "Kart kaydedilemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _navigator.GoBackAsync(cancellationToken);

    private bool Validate(out CreateCardRequest? request)
    {
        request = null;
        ErrorMessage = null;

        var bankName = BankName.Trim();
        if (bankName.Length == 0)
        {
            ErrorMessage = "Banka adı zorunludur.";
            return false;
        }

        if (bankName.Length > NameMaxLength)
        {
            ErrorMessage = $"Banka adı en fazla {NameMaxLength} karakter olabilir.";
            return false;
        }

        var displayName = DisplayName.Trim();
        if (displayName.Length == 0)
        {
            ErrorMessage = "Kart adı zorunludur.";
            return false;
        }

        if (displayName.Length > NameMaxLength)
        {
            ErrorMessage = $"Kart adı en fazla {NameMaxLength} karakter olabilir.";
            return false;
        }

        decimal? creditLimit = null;
        if (!string.IsNullOrWhiteSpace(CreditLimitText))
        {
            if (!InputValidation.TryParseAmount(CreditLimitText, out var parsedLimit))
            {
                ErrorMessage = "Geçerli bir kredi limiti giriniz.";
                return false;
            }

            if (parsedLimit > MaxCreditLimit)
            {
                ErrorMessage = "Kredi limiti izin verilen üst sınırı aşıyor.";
                return false;
            }

            if (decimal.Round(parsedLimit, CreditLimitScale) != parsedLimit)
            {
                ErrorMessage = $"Kredi limiti en fazla {CreditLimitScale} ondalık basamak içerebilir.";
                return false;
            }

            creditLimit = parsedLimit;
        }

        if (!TryParseOptionalDay(StatementDayText, out var statementDay, out var statementError))
        {
            ErrorMessage = $"Ekstre günü {statementError}";
            return false;
        }

        if (!TryParseOptionalDay(PaymentDueDayText, out var paymentDueDay, out var paymentDueError))
        {
            ErrorMessage = $"Son ödeme günü {paymentDueError}";
            return false;
        }

        request = new CreateCardRequest(bankName, displayName, creditLimit, statementDay, paymentDueDay);
        return true;
    }

    private static bool TryParseOptionalDay(
        string? text,
        out int? day,
        out string error)
    {
        day = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(
                text.Trim(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed is < MinDay or > MaxDay)
        {
            error = $"{MinDay} ile {MaxDay} arasında olmalıdır.";
            return false;
        }

        day = parsed;
        return true;
    }
}
