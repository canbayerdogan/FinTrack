namespace FinTrack.Modules.CashInflows.Endpoints;

public sealed record CreateCashInflowRequest(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    string? Source,
    string? Description,
    bool? IsEarnedIncome);

public sealed record UpdateCashInflowRequest(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    string? Source,
    string? Description,
    string? RowVersion,
    DateTime? RecordedAtUtc,
    bool? IsEarnedIncome);

public sealed record CancelRequest(string? Reason, string? RowVersion);
