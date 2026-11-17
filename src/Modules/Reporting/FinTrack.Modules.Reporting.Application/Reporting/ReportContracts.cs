namespace FinTrack.Modules.Reporting.Application.Reporting;

// --- Queries -----------------------------------------------------------------

public sealed record GetMonthlyReportQuery(int Year, int Month);

public sealed record GetDebtSummaryQuery;

public sealed record GetUpcomingPaymentsQuery(int Days);

// --- Monthly report -----------------------------------------------------------

public sealed record ReportPeriodDto(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    string TimeZone);

public sealed record OtherInflowItemDto(string Source, decimal Amount);

public sealed record OtherInflowsDto(
    decimal Total,
    IReadOnlyList<OtherInflowItemDto> Items);

public sealed record UnknownInflowsDto(decimal Total, int Count);

public sealed record ExpenseSummaryDto(decimal Gross, decimal Refunds, decimal Net);

public sealed record CategoryBreakdownDto(
    Guid CategoryId,
    string CategoryName,
    decimal NetAmount);

public sealed record MonthlyReportDto(
    ReportPeriodDto Period,
    string Currency,
    decimal EarnedIncome,
    OtherInflowsDto OtherInflows,
    UnknownInflowsDto UnknownInflows,
    ExpenseSummaryDto Expense,
    decimal IncomeExpenseDifference,
    bool IsBankBalance,
    string Disclaimer,
    IReadOnlyList<CategoryBreakdownDto> Categories);

// --- Debt summary -------------------------------------------------------------

public sealed record CardDebtDto(
    Guid CardId,
    string DisplayName,
    decimal Debt,
    bool IsInCredit);

public sealed record LoanDebtDto(
    Guid LoanId,
    string? BankName,
    decimal RemainingAmount);

public sealed record PersonalDebtDto(
    Guid PersonalDebtId,
    string CounterpartyName,
    decimal RemainingAmount);

public sealed record DebtSummaryDto(
    DateTime AsOfUtc,
    string Currency,
    decimal TotalDebt,
    decimal CardCreditTotal,
    IReadOnlyList<CardDebtDto> Cards,
    IReadOnlyList<LoanDebtDto> Loans,
    IReadOnlyList<PersonalDebtDto> PersonalDebts);

// --- Upcoming payments --------------------------------------------------------

public sealed record UpcomingPaymentItemDto(
    string Type,
    Guid? LoanId,
    int? InstallmentNumber,
    Guid? CardId,
    Guid? StatementId,
    DateOnly DueDate,
    decimal Amount);

public sealed record UpcomingPaymentsDto(
    DateOnly From,
    DateOnly To,
    string Currency,
    IReadOnlyList<UpcomingPaymentItemDto> Items);
