using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.Loans;

/// <summary>
/// Krediyi ve bağlı <c>BankLoan</c> para girişini aynı kullanıcı işleminde
/// oluşturur (docs/05 §6.1, ADR-0009). Transaction sahibi handler'dır.
/// </summary>
public sealed class CreateLoanHandler(
    ICurrentUser currentUser,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CreateLoanCommand, Result<LoanDto>>
{
    public async Task<Result<LoanDto>> HandleAsync(
        CreateLoanCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        var createResult = Loan.Create(
            userId,
            command.Principal,
            command.MonthlyPayment,
            command.InstallmentCount,
            command.FirstDueDate,
            command.BankName,
            command.Description,
            command.Currency,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<LoanDto>(createResult.Error);
        }

        var loan = createResult.Value;
        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);
            await loans.AddAsync(loan, cancellationToken);

            var inflowResult = await inflowWriter.CreateAsync(
                new DebtInflowCreate(
                    userId,
                    loan.Principal,
                    loan.Currency,
                    today,
                    DebtInflowKind.Loan,
                    loan.Id,
                    loan.Description,
                    today,
                    clock.UtcNow),
                scope,
                cancellationToken);
            if (inflowResult.IsFailure)
            {
                return Result.Failure<LoanDto>(inflowResult.Error);
            }

            var linkResult = loan.LinkCashInflow(inflowResult.Value);
            if (linkResult.IsFailure)
            {
                return Result.Failure<LoanDto>(linkResult.Error);
            }

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (outcome == SaveChangesOutcome.ConcurrencyConflict)
            {
                return Result.Failure<LoanDto>(ApplicationErrors.Concurrency());
            }

            if (outcome == SaveChangesOutcome.DuplicateKey)
            {
                return Result.Failure<LoanDto>(ApplicationErrors.DuplicateKey());
            }

            await scope.CommitAsync(cancellationToken);
            return Result.Success(LoanMapper.ToDto(loan));
        }
        finally
        {
            unitOfWork.Enlist(null);
            await scope.DisposeAsync();
        }
    }
}

/// <summary>
/// Krediyi ve bağlı para girişini birlikte iptal eder. Ödenmiş taksit varsa
/// domain katmanı <c>StateConflict</c> döner (docs/06 §7.1).
/// </summary>
public sealed class CancelLoanHandler(
    ICurrentUser currentUser,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CancelLoanCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelLoanCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var loan = await loans.FindAsync(userId, command.LoanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure(ApplicationErrors.LoanNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(loan.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        if (loan.LinkedCashInflowId == Guid.Empty)
        {
            return Result.Failure(ApplicationErrors.LoanInflowMissing());
        }

        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);

            var cancelResult = loan.Cancel(command.Reason, clock.UtcNow);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            var inflowResult = await inflowWriter.CancelAsync(
                new DebtInflowCancel(
                    userId,
                    loan.LinkedCashInflowId,
                    DebtInflowKind.Loan,
                    loan.Id,
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

/// <summary>
/// Sıradaki taksiti tam tutarıyla öder. Eşzamanlılık kredi aggregate'inin
/// <c>rowVersion</c> değeriyle korunur; ödeme gider değildir (docs/05 §6.3).
/// </summary>
public sealed class PayInstallmentHandler(
    ICurrentUser currentUser,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<PayInstallmentCommand, Result<LoanDto>>
{
    public async Task<Result<LoanDto>> HandleAsync(
        PayInstallmentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var loan = await loans.FindAsync(userId, command.LoanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanDto>(ApplicationErrors.LoanNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure<LoanDto>(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(loan.RowVersion, requestedRowVersion))
        {
            return Result.Failure<LoanDto>(ApplicationErrors.Concurrency());
        }

        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);
        var payResult = loan.PayInstallment(command.Number, command.PaidOn, today, clock.UtcNow);
        if (payResult.IsFailure)
        {
            return Result.Failure<LoanDto>(payResult.Error);
        }

        await loans.AddPaymentAsync(payResult.Value, cancellationToken);

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<LoanDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<LoanDto>(ApplicationErrors.DuplicateKey());
        }

        return Result.Success(LoanMapper.ToDto(loan));
    }
}

/// <summary>
/// Ödenmiş bir taksitin ödemesini iptal eder. <c>rowVersion</c> alt kayda,
/// yani taksit ödemesine aittir (docs/06 §1.5, §7.1).
/// </summary>
public sealed class CancelInstallmentPaymentHandler(
    ICurrentUser currentUser,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CancelInstallmentPaymentCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelInstallmentPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var loan = await loans.FindAsync(userId, command.LoanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure(ApplicationErrors.LoanNotFound());
        }

        var installment = loan.Installments.FirstOrDefault(i => i.Number == command.Number);
        var payment = installment?.ActivePayment;
        if (payment is null || payment.Id != command.PaymentId)
        {
            return Result.Failure(ApplicationErrors.InstallmentPaymentNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(payment.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = loan.CancelInstallmentPayment(
            command.Number,
            command.PaymentId,
            command.Reason,
            clock.UtcNow);
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
