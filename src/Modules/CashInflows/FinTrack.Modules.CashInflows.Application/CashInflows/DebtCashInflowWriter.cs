using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Application.CashInflows;

/// <summary>
/// ADR-0009 sync writer uygulaması. Yalnızca borç sahibi modüller tarafından,
/// açılmış bir <see cref="IWriteTransactionScope"/> ile çağrılır; kendi
/// transaction'ını başlatmaz ve commit etmez.
/// </summary>
public sealed class DebtCashInflowWriter(
    ICashInflowRepository inflows,
    IUnitOfWork unitOfWork) : IDebtCashInflowWriter
{
    public async Task<Result<Guid>> CreateAsync(
        DebtInflowCreate request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken)
    {
        var source = MapSource(request.Kind);
        if (source is null)
        {
            return Result.Failure<Guid>(ApplicationErrors.DebtInflowKindInvalid());
        }

        var createResult = CashInflow.CreateFromDebtOwner(
            request.UserId,
            request.Amount,
            request.Currency,
            request.OccurredOn,
            source.Value,
            request.DebtLinkId,
            request.Description,
            request.Today,
            request.NowUtc);

        if (createResult.IsFailure)
        {
            return Result.Failure<Guid>(createResult.Error);
        }

        try
        {
            unitOfWork.Enlist(scope);

            var inflow = createResult.Value;
            await inflows.AddAsync(inflow, cancellationToken);

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            return outcome switch
            {
                SaveChangesOutcome.DuplicateKey => Result.Failure<Guid>(ApplicationErrors.DuplicateKey()),
                SaveChangesOutcome.ConcurrencyConflict => Result.Failure<Guid>(ApplicationErrors.Concurrency()),
                _ => Result.Success(inflow.Id),
            };
        }
        finally
        {
            // Transaction sahibi modülde kapanır; DbContext yine de referansı
            // bırakmalı, aksi halde aynı scope sonrası sorgular bozulur.
            unitOfWork.Enlist(null);
        }
    }

    public async Task<Result> CancelAsync(
        DebtInflowCancel request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken)
    {
        var source = MapSource(request.Kind);
        if (source is null)
        {
            return Result.Failure(ApplicationErrors.DebtInflowKindInvalid());
        }

        try
        {
            unitOfWork.Enlist(scope);

            var inflow = await inflows.FindAsync(request.UserId, request.CashInflowId, cancellationToken);
            if (inflow is null)
            {
                return Result.Failure(ApplicationErrors.CashInflowNotFound());
            }

            if (inflow.DebtType is null || inflow.DebtLinkId is null)
            {
                return Result.Failure(ApplicationErrors.DebtInflowLinkMissing());
            }

            if (inflow.Source != source.Value || inflow.DebtLinkId != request.DebtLinkId)
            {
                return Result.Failure(ApplicationErrors.DebtInflowLinkMismatch());
            }

            var cancelResult = inflow.CancelFromDebtOwner(request.Reason, request.NowUtc);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            return outcome switch
            {
                SaveChangesOutcome.DuplicateKey => Result.Failure(ApplicationErrors.DuplicateKey()),
                SaveChangesOutcome.ConcurrencyConflict => Result.Failure(ApplicationErrors.Concurrency()),
                _ => Result.Success(),
            };
        }
        finally
        {
            unitOfWork.Enlist(null);
        }
    }

    private static CashInflowSource? MapSource(DebtInflowKind kind) => kind switch
    {
        DebtInflowKind.CashAdvance => CashInflowSource.CashAdvance,
        DebtInflowKind.Loan => CashInflowSource.BankLoan,
        DebtInflowKind.PersonalDebt => CashInflowSource.PersonalBorrowing,
        _ => null,
    };
}
