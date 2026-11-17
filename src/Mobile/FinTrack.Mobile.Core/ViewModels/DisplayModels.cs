namespace FinTrack.Mobile.Core.ViewModels;

/// <summary>
/// XAML'de doğrudan bağlanabilen, biçimlendirilmiş liste satırları.
/// </summary>
public sealed record ExpenseListItem(
    string Id,
    string Date,
    string Category,
    string Description,
    string Amount,
    string Status);

public sealed record CardListItem(
    string Id,
    string DisplayName,
    string BankName,
    string Limit,
    string Status);

public sealed record StatementListItem(
    string Id,
    string Period,
    string DueDate,
    string Amount,
    string Status);

public sealed record LoanListItem(
    string Id,
    string Title,
    string Remaining,
    string Status);

public sealed record DebtorListItem(
    string Id,
    string Name,
    string Remaining,
    string Status);

public sealed record CategoryAmountItem(string Name, string Amount);

public sealed record UpcomingPaymentItem(string Type, string DueDate, string Amount, string Detail);
