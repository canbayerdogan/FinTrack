using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Application.CashInflows;

public sealed record DebtLinkDto(DebtLinkType Type, Guid Id);

public sealed record CashInflowDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    CashInflowSource Source,
    bool? IsEarnedIncome,
    string? Description,
    CashInflowStatus Status,
    DebtLinkDto? DebtLink,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record CashInflowFilter(
    DateOnly? From,
    DateOnly? To,
    CashInflowSource? Source,
    CashInflowStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListCashInflowsQuery(CashInflowFilter Filter);

public sealed record GetCashInflowQuery(Guid Id);

public sealed record CreateCashInflowCommand(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    CashInflowSource? Source,
    string? Description);

public sealed record UpdateCashInflowCommand(
    Guid Id,
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    CashInflowSource? Source,
    string? Description,
    string? RowVersion);

public sealed record CancelCashInflowCommand(Guid Id, string? Reason, string? RowVersion);

public static class CashInflowMapper
{
    public static CashInflowDto ToDto(CashInflow inflow) => new(
        inflow.Id,
        inflow.Amount,
        inflow.Currency,
        inflow.OccurredOn,
        inflow.Source,
        inflow.IsEarnedIncome,
        inflow.Description,
        inflow.Status,
        inflow.DebtType is { } type && inflow.DebtLinkId is { } id
            ? new DebtLinkDto(type, id)
            : null,
        inflow.RecordedAtUtc,
        RowVersionCodec.ToBase64(inflow.RowVersion));
}
