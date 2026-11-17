using System.Text.Json.Serialization;

namespace FinTrack.Modules.Debts.Endpoints;

// Türetilmiş alanlar istemci girdisi değildir (docs/06 §7). Bilinmeyen alan
// gönderilirse 400 döner; genel JSON davranışı değiştirilmez.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLoanRequest(
    decimal Principal,
    decimal MonthlyPayment,
    int InstallmentCount,
    DateOnly FirstDueDate,
    string? BankName,
    string? Description,
    string? Currency);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayInstallmentRequest(DateOnly PaidOn, string? RowVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePersonalDebtRequest(
    string? CounterpartyName,
    decimal Principal,
    DateOnly BorrowedOn,
    DateOnly? DueDate,
    string? Description,
    string? Currency);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddRepaymentRequest(decimal Amount, DateOnly PaidOn, string? Note);

public sealed record CancelRequest(string? Reason, string? RowVersion);
