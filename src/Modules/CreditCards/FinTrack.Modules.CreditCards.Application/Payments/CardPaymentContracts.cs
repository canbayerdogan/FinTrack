using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Payments;

/// <summary>Kart ödemesi yanıtı (docs/06 §6.4). İptal gerekçesi DTO'da dönmez.</summary>
public sealed record CardPaymentDto(
    Guid Id,
    Guid CardId,
    decimal Amount,
    string Currency,
    DateOnly PaidOn,
    string? Note,
    PaymentStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record CardPaymentFilter(
    PaymentStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListCardPaymentsQuery(Guid CardId, CardPaymentFilter Filter);

public sealed record CreateCardPaymentCommand(
    Guid CardId,
    decimal Amount,
    string? Currency,
    DateOnly? PaidOn,
    string? Note);

public sealed record CancelCardPaymentCommand(
    Guid CardId,
    Guid PaymentId,
    string? Reason,
    string? RowVersion);

public static class CardPaymentMapper
{
    public static CardPaymentDto ToDto(CardPayment payment) => new(
        payment.Id,
        payment.CardId,
        payment.Amount,
        payment.Currency,
        payment.PaidOn,
        payment.Note,
        payment.Status,
        payment.RecordedAtUtc,
        RowVersionCodec.ToBase64(payment.RowVersion));
}
