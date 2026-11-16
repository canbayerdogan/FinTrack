using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Statements;

/// <summary>
/// Ekstre yanıtı (docs/06 §6.6). <c>CarriedOverAmount</c>,
/// <c>ComputedDebtAtPeriodEnd</c> ve <c>ReconciliationDifference</c> türetilir;
/// okuma anında hesaplanır, saklanmaz.
/// </summary>
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
    StatementStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

/// <summary>
/// Ekstre okuma projeksiyonu. Türetilen alanları içermez; onları Application
/// katmanı hesaplar. Infrastructure yalnızca bu ham kaydı döner.
/// </summary>
public sealed record StatementRecord(
    Guid Id,
    Guid CardId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    decimal StatementAmount,
    string Currency,
    DateTime RecordedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    byte[] RowVersion);

public sealed record StatementFilter(
    StatementStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListStatementsQuery(Guid CardId, StatementFilter Filter);

public sealed record GetStatementQuery(Guid CardId, Guid StatementId);

public sealed record CreateStatementCommand(
    Guid CardId,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateOnly? DueDate,
    decimal StatementAmount,
    string? Currency);

public sealed record CancelStatementCommand(
    Guid CardId,
    Guid StatementId,
    string? Reason,
    string? RowVersion);

public static class StatementMapper
{
    public static StatementDto ToDto(
        StatementRecord record,
        decimal carriedOverAmount,
        decimal computedDebtAtPeriodEnd) => new(
        record.Id,
        record.CardId,
        record.PeriodStart,
        record.PeriodEnd,
        record.DueDate,
        record.StatementAmount,
        record.Currency,
        carriedOverAmount,
        computedDebtAtPeriodEnd,
        record.StatementAmount - computedDebtAtPeriodEnd,
        record.CancelledAtUtc is null ? StatementStatus.Active : StatementStatus.Cancelled,
        record.RecordedAtUtc,
        RowVersionCodec.ToBase64(record.RowVersion));

    /// <summary>Yeni yazılmış aggregate'i okuma projeksiyonuna çevirir.</summary>
    public static StatementRecord ToRecord(Statement statement) => new(
        statement.Id,
        statement.CardId,
        statement.PeriodStart,
        statement.PeriodEnd,
        statement.DueDate,
        statement.StatementAmount,
        statement.Currency,
        statement.RecordedAtUtc,
        statement.CancelledAtUtc,
        statement.CancellationReason,
        statement.RowVersion);
}
