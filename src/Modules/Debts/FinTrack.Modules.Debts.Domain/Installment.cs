using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>
/// Kredi vade planının bir taksiti (docs/05 §6.2). Tutarı kredinin
/// <c>MonthlyPayment</c> değerine eşittir; son taksite yuvarlama farkı eklenmez.
/// </summary>
public sealed class Installment : Entity
{
    private readonly List<InstallmentPayment> _payments = [];

    private Installment()
    {
    }

    public Guid UserId { get; private set; }

    public Guid LoanId { get; private set; }

    public int Number { get; private set; }

    public DateOnly DueDate { get; private set; }

    public decimal Amount { get; private set; }

    public InstallmentStatus Status { get; private set; }

    public IReadOnlyCollection<InstallmentPayment> Payments => _payments;

    /// <summary>Aktif ödeme kaydı; iptal edilmiş ödemeler geçmişte kalır.</summary>
    public InstallmentPayment? ActivePayment => _payments.FirstOrDefault(p => p.Status == DebtPaymentStatus.Active);

    internal static Installment Plan(
        Guid userId,
        Guid loanId,
        int number,
        DateOnly dueDate,
        decimal amount) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoanId = loanId,
            Number = number,
            DueDate = dueDate,
            Amount = amount,
            Status = InstallmentStatus.Planned,
        };

    /// <summary>
    /// Tam taksit tutarıyla ödeme. Sıra kontrolü aggregate kökü <see cref="Loan"/>
    /// tarafından yapılır; burada yalnızca taksitin kendi durumu korunur.
    /// </summary>
    public Result<InstallmentPayment> Pay(DateOnly paidOn, DateOnly today, DateTime nowUtc)
    {
        if (Status == InstallmentStatus.Cancelled)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.InstallmentCancelled());
        }

        if (Status == InstallmentStatus.Paid)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.InstallmentAlreadyPaid());
        }

        if (paidOn == default)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.PaymentDateRequired());
        }

        if (paidOn > today)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.PaymentDateInFuture());
        }

        var payment = InstallmentPayment.Start(UserId, Id, Amount, paidOn, nowUtc);
        _payments.Add(payment);
        Status = InstallmentStatus.Paid;
        return Result.Success(payment);
    }

    /// <summary>
    /// Ödeme iptali taksiti tekrar <see cref="InstallmentStatus.Planned"/> yapar.
    /// Son ödenmiş taksit kısıtı yoktur (2026-11-17 onayı).
    /// </summary>
    public Result CancelPayment(Guid paymentId, string? reason, DateTime nowUtc)
    {
        var payment = _payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null)
        {
            return Result.Failure(DomainErrors.InstallmentPaymentNotFound());
        }

        if (payment.Status != DebtPaymentStatus.Active)
        {
            return Result.Failure(DomainErrors.InstallmentPaymentAlreadyCancelled());
        }

        var cancelResult = payment.Cancel(reason, nowUtc);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        Status = InstallmentStatus.Planned;
        return Result.Success();
    }
}
