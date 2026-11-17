using System.Text.Json.Serialization;

namespace FinTrack.Modules.CreditCards.Endpoints;

// Kart isteklerinde bilinmeyen alan kabul edilmez (docs/06 §6.2): gerçek kart
// numarası, CVV veya son kullanma tarihi gönderilirse 400 döner. Kural yalnızca
// kart uç noktalarına uygulanır; genel JSON davranışı değiştirilmez.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCardRequest(
    string? BankName,
    string? DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCardRequest(
    string? BankName,
    string? DisplayName,
    string? LogoKey,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay,
    string? RowVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArchiveCardRequest(string? RowVersion);

public sealed record CreateCardPaymentRequest(
    decimal Amount,
    string? Currency,
    DateOnly? PaidOn,
    string? Note);

public sealed record CreateCashAdvanceRequest(
    decimal Amount,
    string? Currency,
    DateOnly? TakenOn,
    string? Description);

// Ekstre türetilen alanları (carriedOverAmount, computedDebtAtPeriodEnd,
// reconciliationDifference) istemci gönderemez; bilinmeyen alan 400 döner
// (docs/06 §6.6).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateStatementRequest(
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateOnly? DueDate,
    decimal StatementAmount,
    string? Currency);

public sealed record CancelRequest(string? Reason, string? RowVersion);
