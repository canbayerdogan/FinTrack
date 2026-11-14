using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Expenses;

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    Guid CategoryId,
    string CategoryName,
    string? Description,
    PaymentMethod PaymentMethod,
    Guid? CardId,
    ExpenseStatus Status,
    decimal RefundedAmount,
    decimal NetAmount,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record RefundDto(
    Guid Id,
    Guid ExpenseId,
    decimal Amount,
    string Currency,
    DateOnly RefundedOn,
    string? Description,
    RefundStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record ExpenseFilter(
    DateOnly? From,
    DateOnly? To,
    Guid? CategoryId,
    Guid? CardId,
    PaymentMethod? PaymentMethod,
    ExpenseStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListExpensesQuery(ExpenseFilter Filter);

public sealed record GetExpenseQuery(Guid Id);

public sealed record ListRefundsQuery(Guid ExpenseId);

public sealed record CreateExpenseCommand(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    PaymentMethod? PaymentMethod,
    Guid? CardId);

public sealed record UpdateExpenseCommand(
    Guid Id,
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    PaymentMethod? PaymentMethod,
    Guid? CardId,
    string? RowVersion);

public sealed record CancelExpenseCommand(Guid Id, string? Reason, string? RowVersion);

public sealed record AddRefundCommand(
    Guid ExpenseId,
    decimal Amount,
    string? Currency,
    DateOnly? RefundedOn,
    string? Description);

public sealed record CancelRefundCommand(
    Guid ExpenseId,
    Guid RefundId,
    string? Reason,
    string? RowVersion);

public static class ExpenseMapper
{
    public static ExpenseDto ToDto(Expense expense, string categoryName) => new(
        expense.Id,
        expense.Amount,
        expense.Currency,
        expense.OccurredOn,
        expense.CategoryId,
        categoryName,
        expense.Description,
        expense.PaymentMethod,
        expense.CardId,
        expense.Status,
        expense.RefundedAmount,
        expense.NetAmount,
        expense.RecordedAtUtc,
        RowVersionCodec.ToBase64(expense.RowVersion));

    public static RefundDto ToDto(Refund refund) => new(
        refund.Id,
        refund.ExpenseId,
        refund.Amount,
        refund.Currency,
        refund.RefundedOn,
        refund.Description,
        refund.Status,
        refund.RecordedAtUtc,
        RowVersionCodec.ToBase64(refund.RowVersion));
}
