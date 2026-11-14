using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Cards;

public sealed record CardDto(
    Guid Id,
    string BankName,
    string DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay,
    CardStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

/// <summary>Bakiye kırılımı (docs/06 §6.3). Slice 3'te ödeme/avans terimleri sıfırdır.</summary>
public sealed record CardBalanceBreakdownDto(
    decimal Expenses,
    decimal Refunds,
    decimal CashAdvances,
    decimal Payments);

/// <summary>
/// Hesaplanan kart borcu. Negatif <c>Debt</c> hata değildir; kart lehine bakiye
/// (alacak) anlamına gelir ve <c>IsInCredit</c> ile işaretlenir.
/// </summary>
public sealed record CardBalanceDto(
    Guid CardId,
    DateTime AsOfUtc,
    string Currency,
    decimal Debt,
    bool IsInCredit,
    CardBalanceBreakdownDto Breakdown);

public sealed record CardFilter(CardStatus? Status);

public sealed record ListCardsQuery(CardFilter Filter);

public sealed record GetCardQuery(Guid Id);

public sealed record GetCardBalanceQuery(Guid Id);

public sealed record CreateCardCommand(
    string? BankName,
    string? DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay);

public sealed record UpdateCardCommand(
    Guid Id,
    string? BankName,
    string? DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay,
    string? RowVersion);

public sealed record ArchiveCardCommand(Guid Id, string? RowVersion);

public static class CardMapper
{
    public static CardDto ToDto(CreditCard card) => new(
        card.Id,
        card.BankName,
        card.DisplayName,
        card.LogoKey,
        card.CreditLimit,
        card.StatementDay,
        card.PaymentDueDay,
        card.Status,
        card.CreatedAtUtc,
        RowVersionCodec.ToBase64(card.RowVersion));
}
