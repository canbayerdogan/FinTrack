using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.CashAdvances;

/// <summary>Nakit avans yanıtı (docs/06 §6.5). İptal gerekçesi DTO'da dönmez.</summary>
public sealed record CashAdvanceDto(
    Guid Id,
    Guid CardId,
    decimal Amount,
    string Currency,
    DateOnly TakenOn,
    string? Description,
    CashAdvanceStatus Status,
    Guid? LinkedCashInflowId,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record CashAdvanceFilter(
    CashAdvanceStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListCashAdvancesQuery(Guid CardId, CashAdvanceFilter Filter);

public sealed record CreateCashAdvanceCommand(
    Guid CardId,
    decimal Amount,
    string? Currency,
    DateOnly? TakenOn,
    string? Description);

public sealed record CancelCashAdvanceCommand(
    Guid CardId,
    Guid CashAdvanceId,
    string? Reason,
    string? RowVersion);

public static class CashAdvanceMapper
{
    public static CashAdvanceDto ToDto(CashAdvance advance) => new(
        advance.Id,
        advance.CardId,
        advance.Amount,
        advance.Currency,
        advance.TakenOn,
        advance.Description,
        advance.Status,
        advance.LinkedCashInflowId,
        advance.RecordedAtUtc,
        RowVersionCodec.ToBase64(advance.RowVersion));
}
