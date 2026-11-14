namespace FinTrack.Modules.Expenses.Endpoints;

public sealed record CreateCategoryRequest(string? Name, string? IconKey);

public sealed record UpdateCategoryRequest(string? Name, string? IconKey, string? RowVersion);

public sealed record ArchiveRequest(string? RowVersion);

public sealed record CancelRequest(string? Reason, string? RowVersion);

public sealed record CreateExpenseRequest(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? PaymentMethod,
    Guid? CardId);

public sealed record UpdateExpenseRequest(
    decimal Amount,
    string? Currency,
    DateOnly? OccurredOn,
    Guid? CategoryId,
    string? Description,
    string? PaymentMethod,
    Guid? CardId,
    string? RowVersion,
    DateTime? RecordedAtUtc);

public sealed record AddRefundRequest(
    decimal Amount,
    string? Currency,
    DateOnly? RefundedOn,
    string? Description);
