using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.PersonalDebts;

/// <summary>
/// Kişisel borcu ve bağlı <c>PersonalBorrowing</c> para girişini aynı kullanıcı
/// işleminde oluşturur (docs/05 §6.5, ADR-0009).
/// </summary>
public sealed class CreatePersonalDebtHandler(
    ICurrentUser currentUser,
    IPersonalDebtRepository debts,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CreatePersonalDebtCommand, Result<PersonalDebtDto>>
{
    public async Task<Result<PersonalDebtDto>> HandleAsync(
        CreatePersonalDebtCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        var createResult = PersonalDebt.Create(
            userId,
            command.CounterpartyName,
            command.Principal,
            command.BorrowedOn,
            command.DueDate,
            command.Description,
            command.Currency,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<PersonalDebtDto>(createResult.Error);
        }

        var debt = createResult.Value;
        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);
            await debts.AddAsync(debt, cancellationToken);

            var inflowResult = await inflowWriter.CreateAsync(
                new DebtInflowCreate(
                    userId,
                    debt.Principal,
                    debt.Currency,
                    debt.BorrowedOn,
                    DebtInflowKind.PersonalDebt,
                    debt.Id,
                    debt.Description,
                    today,
                    clock.UtcNow),
                scope,
                cancellationToken);
            if (inflowResult.IsFailure)
            {
                return Result.Failure<PersonalDebtDto>(inflowResult.Error);
            }

            var linkResult = debt.LinkCashInflow(inflowResult.Value);
            if (linkResult.IsFailure)
            {
                return Result.Failure<PersonalDebtDto>(linkResult.Error);
            }

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (outcome == SaveChangesOutcome.ConcurrencyConflict)
            {
                return Result.Failure<PersonalDebtDto>(ApplicationErrors.Concurrency());
            }

            if (outcome == SaveChangesOutcome.DuplicateKey)
            {
                return Result.Failure<PersonalDebtDto>(ApplicationErrors.DuplicateKey());
            }

            await scope.CommitAsync(cancellationToken);
            return Result.Success(PersonalDebtMapper.ToDto(debt));
        }
        finally
        {
            unitOfWork.Enlist(null);
            await scope.DisposeAsync();
        }
    }
}

/// <summary>
/// Borcu ve bağlı para girişini birlikte iptal eder. Aktif geri ödeme varsa
/// domain katmanı <c>StateConflict</c> döner (docs/06 §7.2).
/// </summary>
public sealed class CancelPersonalDebtHandler(
    ICurrentUser currentUser,
    IPersonalDebtRepository debts,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CancelPersonalDebtCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelPersonalDebtCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var debt = await debts.FindAsync(userId, command.DebtId, cancellationToken);
        if (debt is null)
        {
            return Result.Failure(ApplicationErrors.PersonalDebtNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(debt.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        if (debt.LinkedCashInflowId == Guid.Empty)
        {
            return Result.Failure(ApplicationErrors.PersonalDebtInflowMissing());
        }

        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);

            var cancelResult = debt.Cancel(command.Reason, clock.UtcNow);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            var inflowResult = await inflowWriter.CancelAsync(
                new DebtInflowCancel(
                    userId,
                    debt.LinkedCashInflowId,
                    DebtInflowKind.PersonalDebt,
                    debt.Id,
                    command.Reason!,
                    clock.UtcNow),
                scope,
                cancellationToken);
            if (inflowResult.IsFailure)
            {
                return inflowResult;
            }

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (outcome == SaveChangesOutcome.ConcurrencyConflict)
            {
                return Result.Failure(ApplicationErrors.Concurrency());
            }

            if (outcome == SaveChangesOutcome.DuplicateKey)
            {
                return Result.Failure(ApplicationErrors.DuplicateKey());
            }

            await scope.CommitAsync(cancellationToken);
            return Result.Success();
        }
        finally
        {
            unitOfWork.Enlist(null);
            await scope.DisposeAsync();
        }
    }
}

/// <summary>Geri ödeme borcu azaltır; gider kaydı oluşturmaz (docs/05 §6.6).</summary>
public sealed class AddRepaymentHandler(
    ICurrentUser currentUser,
    IPersonalDebtRepository debts,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<AddRepaymentCommand, Result<RepaymentDto>>
{
    public async Task<Result<RepaymentDto>> HandleAsync(
        AddRepaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var debt = await debts.FindAsync(userId, command.DebtId, cancellationToken);
        if (debt is null)
        {
            return Result.Failure<RepaymentDto>(ApplicationErrors.PersonalDebtNotFound());
        }

        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);
        var addResult = debt.AddRepayment(command.Amount, command.PaidOn, command.Note, today, clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<RepaymentDto>(addResult.Error);
        }

        await debts.AddRepaymentAsync(addResult.Value, cancellationToken);

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<RepaymentDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<RepaymentDto>(ApplicationErrors.DuplicateKey());
        }

        return Result.Success(PersonalDebtMapper.ToRepaymentDto(addResult.Value));
    }
}

/// <summary>Geri ödeme iptali kalan tutarı geri yükseltir; alt kaydın sürümü esas alınır.</summary>
public sealed class CancelRepaymentHandler(
    ICurrentUser currentUser,
    IPersonalDebtRepository debts,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CancelRepaymentCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelRepaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var debt = await debts.FindAsync(userId, command.DebtId, cancellationToken);
        if (debt is null)
        {
            return Result.Failure(ApplicationErrors.PersonalDebtNotFound());
        }

        var repayment = debt.Repayments.FirstOrDefault(r =>
            r.Id == command.RepaymentId && r.Status == DebtPaymentStatus.Active);
        if (repayment is null)
        {
            return Result.Failure(ApplicationErrors.RepaymentNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(repayment.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = debt.CancelRepayment(command.RepaymentId, command.Reason, clock.UtcNow);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure(ApplicationErrors.DuplicateKey());
        }

        return Result.Success();
    }
}
