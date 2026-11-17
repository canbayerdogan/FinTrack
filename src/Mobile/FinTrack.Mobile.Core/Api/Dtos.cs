namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// docs/06 v1 sözleşmelerinin istemci tarafındaki okuma modelleri.
/// Alan adları camelCase JSON ile eşleşir; sunucu tarafı doğrulama otoritesidir.
/// </summary>
public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string PreferredTimeZone,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);

public sealed record ProfileResponse(
    Guid UserId,
    string Email,
    string PreferredTimeZone,
    string RowVersion);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? IconKey,
    bool IsArchived,
    string RowVersion)
{
    // Picker, ItemDisplayBinding olmadan da okunabilir ad gösterir (XC0022 yok).
    public override string ToString() => Name;
}

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    Guid CategoryId,
    string CategoryName,
    string? Description,
    string PaymentMethod,
    Guid? CardId,
    string Status,
    decimal RefundedAmount,
    decimal NetAmount,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record CardDto(
    Guid Id,
    string BankName,
    string DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay,
    string Status,
    DateTime RecordedAtUtc,
    string RowVersion)
{
    // Picker, ItemDisplayBinding olmadan da okunabilir ad gösterir (XC0022 yok).
    public override string ToString() => DisplayName;
}

public sealed record BalanceBreakdown(
    decimal Expenses,
    decimal Refunds,
    decimal CashAdvances,
    decimal Payments);

public sealed record CardBalanceDto(
    Guid CardId,
    DateOnly AsOfDate,
    string Currency,
    decimal Debt,
    bool IsInCredit,
    BalanceBreakdown Breakdown);

public sealed record StatementDto(
    Guid Id,
    Guid CardId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    decimal StatementAmount,
    string Currency,
    decimal CarriedOverAmount,
    decimal ComputedDebtAtPeriodEnd,
    decimal ReconciliationDifference,
    string Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record LoanPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaidOn,
    string Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record LoanInstallmentDto(
    int Number,
    DateOnly DueDate,
    decimal Amount,
    string Status,
    DateOnly? PaidOn,
    LoanPaymentDto? Payment);

public sealed record LoanDto(
    Guid Id,
    decimal Principal,
    decimal MonthlyPayment,
    int InstallmentCount,
    decimal TotalRepayment,
    DateOnly FirstDueDate,
    string? BankName,
    string? Description,
    string Currency,
    string Status,
    decimal RemainingAmount,
    DateTime RecordedAtUtc,
    string RowVersion,
    IReadOnlyList<LoanInstallmentDto> Installments);

public sealed record RepaymentDto(
    Guid Id,
    Guid PersonalDebtId,
    decimal Amount,
    string Currency,
    DateOnly PaidOn,
    string? Note,
    string Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record PersonalDebtDto(
    Guid Id,
    string CounterpartyName,
    decimal Principal,
    decimal RemainingAmount,
    DateOnly BorrowedOn,
    DateOnly? DueDate,
    string? Description,
    string Currency,
    string Status,
    DateTime RecordedAtUtc,
    string RowVersion,
    IReadOnlyList<RepaymentDto> Repayments);

public sealed record ReportPeriod(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    string TimeZone);

public sealed record InflowItem(string Source, decimal Amount);

public sealed record InflowBucket(decimal Total, IReadOnlyList<InflowItem> Items);

public sealed record UnknownInflows(decimal Total, int Count);

public sealed record ExpenseSummary(decimal Gross, decimal Refunds, decimal Net);

public sealed record CategoryBreakdown(Guid CategoryId, string CategoryName, decimal NetAmount);

public sealed record MonthlyReport(
    ReportPeriod Period,
    string Currency,
    decimal EarnedIncome,
    InflowBucket OtherInflows,
    UnknownInflows UnknownInflows,
    ExpenseSummary Expense,
    decimal IncomeExpenseDifference,
    bool IsBankBalance,
    string Disclaimer,
    IReadOnlyList<CategoryBreakdown> Categories);

public sealed record CardDebt(Guid CardId, string DisplayName, decimal Debt, bool IsInCredit);

public sealed record LoanDebt(Guid LoanId, string? BankName, decimal RemainingAmount);

public sealed record PersonalDebtBalance(
    Guid PersonalDebtId,
    string CounterpartyName,
    decimal RemainingAmount);

public sealed record DebtSummary(
    DateTime AsOfUtc,
    string Currency,
    decimal TotalDebt,
    decimal CardCreditTotal,
    IReadOnlyList<CardDebt> Cards,
    IReadOnlyList<LoanDebt> Loans,
    IReadOnlyList<PersonalDebtBalance> PersonalDebts);

public sealed record UpcomingPayment(
    string Type,
    Guid? LoanId,
    int? InstallmentNumber,
    Guid? CardId,
    Guid? StatementId,
    DateOnly DueDate,
    decimal Amount);

public sealed record UpcomingPayments(
    DateOnly From,
    DateOnly To,
    string Currency,
    IReadOnlyList<UpcomingPayment> Items);
