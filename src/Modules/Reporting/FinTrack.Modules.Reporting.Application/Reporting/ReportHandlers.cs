using FinTrack.ModuleContracts;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Reporting.Application.Reporting;

internal static class ReportErrors
{
    public static Error InvalidYear() =>
        Error.Validation("InvalidYear", "year 2000 ile 2100 arasında olmalıdır.");

    public static Error InvalidMonth() =>
        Error.Validation("InvalidMonth", "month 1 ile 12 arasında olmalıdır.");

    public static Error InvalidDays() =>
        Error.Validation("InvalidDays", "days 1 ile 90 arasında olmalıdır.");
}

internal static class ReportTime
{
    /// <summary>
    /// Geçersiz/boş token saat dilimi varsayıana düşer; bozuk bir claim raporu
    /// 500'e çevirmez (docs/05 §7.1).
    /// </summary>
    public static string ResolveId(string? preferredTimeZone) =>
        !string.IsNullOrWhiteSpace(preferredTimeZone)
        && TimeZoneInfo.TryFindSystemTimeZoneById(preferredTimeZone.Trim(), out _)
            ? preferredTimeZone.Trim()
            : UserTime.DefaultTimeZoneId;
}

public sealed class GetMonthlyReportHandler(
    ICurrentUser currentUser,
    IExpenseReportQuery expenseQuery,
    ICashInflowReportQuery inflowQuery)
    : IQueryHandler<GetMonthlyReportQuery, Result<MonthlyReportDto>>
{
    public async Task<Result<MonthlyReportDto>> HandleAsync(
        GetMonthlyReportQuery query, CancellationToken cancellationToken)
    {
        if (query.Year is < 2000 or > 2100)
        {
            return Result.Failure<MonthlyReportDto>(ReportErrors.InvalidYear());
        }

        if (query.Month is < 1 or > 12)
        {
            return Result.Failure<MonthlyReportDto>(ReportErrors.InvalidMonth());
        }

        var userId = currentUser.RequireUserId();
        var timeZoneId = ReportTime.ResolveId(currentUser.PreferredTimeZone);
        var from = new DateOnly(query.Year, query.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var expense = await expenseQuery.GetMonthlyAsync(userId, from, to, cancellationToken);
        var inflow = await inflowQuery.GetMonthlyAsync(userId, from, to, cancellationToken);

        var report = new MonthlyReportDto(
            new ReportPeriodDto(query.Year, query.Month, from, to, timeZoneId),
            Money.DefaultCurrency,
            inflow.EarnedIncome,
            new OtherInflowsDto(
                inflow.OtherInflowsTotal,
                inflow.OtherInflows
                    .Select(item => new OtherInflowItemDto(item.Source, item.Amount))
                    .ToArray()),
            new UnknownInflowsDto(inflow.UnknownInflowsTotal, inflow.UnknownInflowsCount),
            new ExpenseSummaryDto(expense.Gross, expense.Refunds, expense.Net),
            inflow.EarnedIncome - expense.Net,
            IsBankBalance: false,
            "Gelir−gider farkı banka bakiyesi değildir.",
            expense.Categories
                .Select(item => new CategoryBreakdownDto(
                    item.CategoryId, item.CategoryName, item.NetAmount))
                .ToArray());

        return Result.Success(report);
    }
}

public sealed class GetDebtSummaryHandler(
    ICurrentUser currentUser,
    IClock clock,
    ICardReportQuery cardQuery,
    IDebtReportQuery debtQuery)
    : IQueryHandler<GetDebtSummaryQuery, Result<DebtSummaryDto>>
{
    public async Task<Result<DebtSummaryDto>> HandleAsync(
        GetDebtSummaryQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var timeZoneId = ReportTime.ResolveId(currentUser.PreferredTimeZone);
        var asOfDate = UserTime.Today(clock, timeZoneId);
        var asOfUtc = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);

        var cards = await cardQuery.GetDebtAsync(userId, asOfDate, timeZoneId, cancellationToken);
        var loans = await debtQuery.GetLoansAsync(userId, cancellationToken);
        var personalDebts = await debtQuery.GetPersonalDebtsAsync(userId, cancellationToken);

        var totalDebt = cards.Cards.Sum(card => Math.Max(0m, card.Debt))
            + loans.Sum(loan => loan.RemainingAmount)
            + personalDebts.Sum(debt => debt.RemainingAmount);

        var report = new DebtSummaryDto(
            asOfUtc,
            Money.DefaultCurrency,
            totalDebt,
            cards.CardCreditTotal,
            cards.Cards.Select(card => new CardDebtDto(
                card.CardId, card.DisplayName, card.Debt, card.IsInCredit)).ToArray(),
            loans.Select(loan => new LoanDebtDto(
                loan.LoanId, loan.BankName, loan.RemainingAmount)).ToArray(),
            personalDebts.Select(debt => new PersonalDebtDto(
                debt.PersonalDebtId, debt.CounterpartyName, debt.RemainingAmount)).ToArray());

        return Result.Success(report);
    }
}

public sealed class GetUpcomingPaymentsHandler(
    ICurrentUser currentUser,
    IClock clock,
    ICardReportQuery cardQuery,
    IDebtReportQuery debtQuery)
    : IQueryHandler<GetUpcomingPaymentsQuery, Result<UpcomingPaymentsDto>>
{
    public async Task<Result<UpcomingPaymentsDto>> HandleAsync(
        GetUpcomingPaymentsQuery query, CancellationToken cancellationToken)
    {
        if (query.Days is < 1 or > 90)
        {
            return Result.Failure<UpcomingPaymentsDto>(ReportErrors.InvalidDays());
        }

        var userId = currentUser.RequireUserId();
        var timeZoneId = ReportTime.ResolveId(currentUser.PreferredTimeZone);
        var from = UserTime.Today(clock, timeZoneId);
        var to = from.AddDays(query.Days);

        var installments = await debtQuery.GetUpcomingInstallmentsAsync(userId, from, to, cancellationToken);
        var statements = await cardQuery.GetStatementDuesAsync(userId, from, to, cancellationToken);

        var items = installments
            .Select(due => new UpcomingPaymentItemDto(
                "LoanInstallment",
                due.LoanId,
                due.InstallmentNumber,
                CardId: null,
                StatementId: null,
                due.DueDate,
                due.Amount))
            .Concat(statements.Select(due => new UpcomingPaymentItemDto(
                "CardStatement",
                LoanId: null,
                InstallmentNumber: null,
                due.CardId,
                due.StatementId,
                due.DueDate,
                due.Amount)))
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ThenBy(item => item.Amount)
            .ToArray();

        return Result.Success(new UpcomingPaymentsDto(from, to, Money.DefaultCurrency, items));
    }
}
