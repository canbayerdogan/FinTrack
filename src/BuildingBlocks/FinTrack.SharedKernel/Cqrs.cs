namespace FinTrack.SharedKernel;

/// <summary>
/// Yazma tarafındaki handler sözleşmesi. MediatR yoktur; handler'lar DI ile
/// doğrudan enjekte edilir (ADR-0003).
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Okuma tarafındaki handler sözleşmesi.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
