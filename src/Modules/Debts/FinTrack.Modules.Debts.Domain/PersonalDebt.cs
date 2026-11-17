using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>
/// Kişisel borç aggregate'i (docs/05 §6.5–6.7). Anapara, borç alınan kişi ve
/// türetilmiş kalan tutar taşır; bağlı para girişi ADR-0009 ile aynı işlemde
/// oluşturulur.
/// </summary>
public sealed class PersonalDebt : AggregateRoot
{
    public const int CounterpartyNameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    private readonly List<PersonalDebtRepayment> _repayments = [];

    private PersonalDebt()
    {
    }

    public Guid UserId { get; private set; }

    public string CounterpartyName { get; private set; } = string.Empty;

    public decimal Principal { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateOnly BorrowedOn { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public string? Description { get; private set; }

    public PersonalDebtStatus Status { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>ADR-0009 ile aynı işlemde oluşturulan para girişinin kimliği.</summary>
    public Guid LinkedCashInflowId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<PersonalDebtRepayment> Repayments => _repayments;

    /// <summary>Kullanıcı set edemez; aktif geri ödemeler toplamı düşülür.</summary>
    public decimal RemainingAmount => Principal - _repayments
        .Where(r => r.Status == DebtPaymentStatus.Active)
        .Sum(r => r.Amount);

    public static Result<PersonalDebt> Create(
        Guid userId,
        string? counterpartyName,
        decimal principal,
        DateOnly borrowedOn,
        DateOnly? dueDate,
        string? description,
        string? currency,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.UserRequired());
        }

        var trimmedCounterparty = string.IsNullOrWhiteSpace(counterpartyName) ? null : counterpartyName.Trim();
        if (trimmedCounterparty is null)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtCounterpartyRequired());
        }

        if (trimmedCounterparty.Length > CounterpartyNameMaxLength)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtCounterpartyTooLong());
        }

        var principalError = DomainErrors.ValidatePositiveAmount(
            principal,
            DomainErrors.PersonalDebtPrincipalInvalid,
            DomainErrors.PersonalDebtPrincipalScale,
            DomainErrors.PersonalDebtPrincipalTooLarge);
        if (principalError is not null)
        {
            return Result.Failure<PersonalDebt>(principalError);
        }

        if (borrowedOn == default)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtBorrowedOnRequired());
        }

        if (borrowedOn > today)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtBorrowedOnInFuture());
        }

        if (dueDate is not null && dueDate < borrowedOn)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtDueDateBeforeBorrowedOn());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure<PersonalDebt>(DomainErrors.PersonalDebtDescriptionTooLong());
        }

        var currencyError = DomainErrors.ValidateCurrency(currency, DomainErrors.PersonalDebtCurrencyUnsupported);
        if (currencyError is not null)
        {
            return Result.Failure<PersonalDebt>(currencyError);
        }

        return Result.Success(new PersonalDebt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CounterpartyName = trimmedCounterparty,
            Principal = principal,
            Currency = DomainErrors.NormalizeCurrency(currency),
            BorrowedOn = borrowedOn,
            DueDate = dueDate,
            Description = trimmedDescription,
            Status = PersonalDebtStatus.Active,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>Writer para girişini aynı transaction'da oluşturduktan sonra bağlanır.</summary>
    public Result LinkCashInflow(Guid cashInflowId)
    {
        if (cashInflowId == Guid.Empty)
        {
            return Result.Failure(DomainErrors.DebtInflowRequired());
        }

        if (LinkedCashInflowId != Guid.Empty)
        {
            return Result.Failure(DomainErrors.DebtInflowAlreadyLinked());
        }

        LinkedCashInflowId = cashInflowId;
        return Result.Success();
    }

    public Result<PersonalDebtRepayment> AddRepayment(
        decimal amount,
        DateOnly paidOn,
        string? note,
        DateOnly today,
        DateTime nowUtc)
    {
        if (Status == PersonalDebtStatus.Cancelled)
        {
            return Result.Failure<PersonalDebtRepayment>(DomainErrors.PersonalDebtCancelled());
        }

        var amountError = DomainErrors.ValidatePositiveAmount(
            amount,
            DomainErrors.RepaymentAmountInvalid,
            DomainErrors.RepaymentAmountScale,
            DomainErrors.RepaymentAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure<PersonalDebtRepayment>(amountError);
        }

        if (paidOn == default)
        {
            return Result.Failure<PersonalDebtRepayment>(DomainErrors.PaymentDateRequired());
        }

        if (paidOn > today)
        {
            return Result.Failure<PersonalDebtRepayment>(DomainErrors.PaymentDateInFuture());
        }

        if (amount > RemainingAmount)
        {
            return Result.Failure<PersonalDebtRepayment>(DomainErrors.RepaymentExceedsRemaining());
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is not null && trimmedNote.Length > PersonalDebtRepayment.NoteMaxLength)
        {
            return Result.Failure<PersonalDebtRepayment>(DomainErrors.RepaymentNoteTooLong());
        }

        var repayment = PersonalDebtRepayment.Start(
            UserId,
            Id,
            amount,
            Currency,
            paidOn,
            trimmedNote,
            nowUtc);
        _repayments.Add(repayment);

        if (RemainingAmount == 0m)
        {
            Status = PersonalDebtStatus.Closed;
        }

        return Result.Success(repayment);
    }

    public Result CancelRepayment(Guid repaymentId, string? reason, DateTime nowUtc)
    {
        if (Status == PersonalDebtStatus.Cancelled)
        {
            return Result.Failure(DomainErrors.PersonalDebtCancelled());
        }

        var repayment = _repayments.FirstOrDefault(r => r.Id == repaymentId);
        if (repayment is null)
        {
            return Result.Failure(DomainErrors.RepaymentNotFound());
        }

        var cancelResult = repayment.Cancel(reason, nowUtc);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (Status == PersonalDebtStatus.Closed)
        {
            Status = PersonalDebtStatus.Active;
        }

        return Result.Success();
    }

    /// <summary>
    /// İptal idempotenttir. Aktif geri ödeme varsa önce o geri ödeme iptal
    /// edilmelidir (docs/05 §6.6).
    /// </summary>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (Status == PersonalDebtStatus.Cancelled)
        {
            return Result.Success();
        }

        if (_repayments.Any(r => r.Status == DebtPaymentStatus.Active))
        {
            return Result.Failure(DomainErrors.PersonalDebtHasActiveRepayments());
        }

        var reasonError = DomainErrors.ValidateReason(reason);
        if (reasonError is not null)
        {
            return Result.Failure(reasonError);
        }

        Status = PersonalDebtStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = reason!.Trim();
        return Result.Success();
    }
}
