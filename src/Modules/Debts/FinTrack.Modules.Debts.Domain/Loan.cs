using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>
/// Taksitli kredi aggregate'i (docs/05 §6.1–6.4). Faiz hesaplanmaz; aylık taksit
/// kullanıcıdan alınır, toplam geri ödeme türetilir. Tüm vade için taksit planı
/// oluşturulur ve bağlı para girişi ADR-0009 ile aynı işlemde yazılır.
/// </summary>
public sealed class Loan : AggregateRoot
{
    public const int BankNameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;
    public const int MaxInstallmentCount = 120;

    private readonly List<Installment> _installments = [];

    private Loan()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>Kullanıcının eline geçen tutar.</summary>
    public decimal Principal { get; private set; }

    public decimal MonthlyPayment { get; private set; }

    public int InstallmentCount { get; private set; }

    public DateOnly FirstDueDate { get; private set; }

    public string? BankName { get; private set; }

    public string? Description { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public LoanStatus Status { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>ADR-0009 ile aynı işlemde oluşturulan para girişinin kimliği.</summary>
    public Guid LinkedCashInflowId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<Installment> Installments => _installments;

    /// <summary>Kullanıcı girdisi değildir; tam çarpımdır.</summary>
    public decimal TotalRepayment => MonthlyPayment * InstallmentCount;

    /// <summary>Planlanan taksitlerin toplamı; ödenen taksitler düşülür.</summary>
    public decimal RemainingAmount => TotalRepayment - _installments
        .Where(i => i.Status == InstallmentStatus.Paid)
        .Sum(i => i.Amount);

    public static Result<Loan> Create(
        Guid userId,
        decimal principal,
        decimal monthlyPayment,
        int installmentCount,
        DateOnly firstDueDate,
        string? bankName,
        string? description,
        string? currency,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<Loan>(DomainErrors.UserRequired());
        }

        var principalError = DomainErrors.ValidatePositiveAmount(
            principal,
            DomainErrors.LoanPrincipalInvalid,
            DomainErrors.LoanPrincipalScale,
            DomainErrors.LoanPrincipalTooLarge);
        if (principalError is not null)
        {
            return Result.Failure<Loan>(principalError);
        }

        var monthlyPaymentError = DomainErrors.ValidatePositiveAmount(
            monthlyPayment,
            DomainErrors.LoanMonthlyPaymentInvalid,
            DomainErrors.LoanMonthlyPaymentScale,
            DomainErrors.LoanMonthlyPaymentTooLarge);
        if (monthlyPaymentError is not null)
        {
            return Result.Failure<Loan>(monthlyPaymentError);
        }

        if (installmentCount is < 1 or > MaxInstallmentCount)
        {
            return Result.Failure<Loan>(DomainErrors.LoanInstallmentCountInvalid());
        }

        if (monthlyPayment * installmentCount < principal)
        {
            return Result.Failure<Loan>(DomainErrors.LoanTotalRepaymentBelowPrincipal());
        }

        if (firstDueDate == default)
        {
            return Result.Failure<Loan>(DomainErrors.LoanFirstDueDateRequired());
        }

        if (firstDueDate < today)
        {
            return Result.Failure<Loan>(DomainErrors.LoanFirstDueDateBeforeToday());
        }

        var trimmedBankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        if (trimmedBankName is not null && trimmedBankName.Length > BankNameMaxLength)
        {
            return Result.Failure<Loan>(DomainErrors.LoanBankNameTooLong());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure<Loan>(DomainErrors.LoanDescriptionTooLong());
        }

        var currencyError = DomainErrors.ValidateCurrency(currency, DomainErrors.LoanCurrencyUnsupported);
        if (currencyError is not null)
        {
            return Result.Failure<Loan>(currencyError);
        }

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Principal = principal,
            MonthlyPayment = monthlyPayment,
            InstallmentCount = installmentCount,
            FirstDueDate = firstDueDate,
            BankName = trimmedBankName,
            Description = trimmedDescription,
            Currency = DomainErrors.NormalizeCurrency(currency),
            Status = LoanStatus.Active,
            RecordedAtUtc = nowUtc,
        };

        for (var number = 1; number <= installmentCount; number++)
        {
            loan._installments.Add(Installment.Plan(
                userId,
                loan.Id,
                number,
                AddMonthsWithDayAnchor(firstDueDate, number - 1),
                monthlyPayment));
        }

        return Result.Success(loan);
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

    public Result<InstallmentPayment> PayInstallment(
        int number,
        DateOnly paidOn,
        DateOnly today,
        DateTime nowUtc)
    {
        if (Status == LoanStatus.Cancelled)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.LoanCancelled());
        }

        var installment = _installments.FirstOrDefault(i => i.Number == number);
        if (installment is null)
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.InstallmentNotFound());
        }

        if (_installments.Any(i => i.Number < number && i.Status != InstallmentStatus.Paid))
        {
            return Result.Failure<InstallmentPayment>(DomainErrors.InstallmentsOutOfOrder());
        }

        var payResult = installment.Pay(paidOn, today, nowUtc);
        if (payResult.IsFailure)
        {
            return payResult;
        }

        if (RemainingAmount == 0m)
        {
            Status = LoanStatus.Closed;
        }

        return payResult;
    }

    public Result CancelInstallmentPayment(int number, Guid paymentId, string? reason, DateTime nowUtc)
    {
        if (Status == LoanStatus.Cancelled)
        {
            return Result.Failure(DomainErrors.LoanCancelled());
        }

        var installment = _installments.FirstOrDefault(i => i.Number == number);
        if (installment is null)
        {
            return Result.Failure(DomainErrors.InstallmentNotFound());
        }

        var cancelResult = installment.CancelPayment(paymentId, reason, nowUtc);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (Status == LoanStatus.Closed)
        {
            Status = LoanStatus.Active;
        }

        return Result.Success();
    }

    /// <summary>
    /// İptal idempotenttir. Ödenmiş taksit varsa önce ödemeler iptal edilmelidir
    /// (docs/06 §7.1).
    /// </summary>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (Status == LoanStatus.Cancelled)
        {
            return Result.Success();
        }

        if (_installments.Any(i => i.Status == InstallmentStatus.Paid))
        {
            return Result.Failure(DomainErrors.LoanHasPaidInstallments());
        }

        var reasonError = DomainErrors.ValidateReason(reason);
        if (reasonError is not null)
        {
            return Result.Failure(reasonError);
        }

        Status = LoanStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = reason!.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Ay günü hedef ayda yoksa ayın son gününe sabitlenir (docs/05 §6.2):
    /// 31 Ocak + 1 ay → 28/29 Şubat, + 2 ay → 31 Mart.
    /// </summary>
    private static DateOnly AddMonthsWithDayAnchor(DateOnly start, int months)
    {
        var absoluteMonth = start.Month + months;
        var year = start.Year + ((absoluteMonth - 1) / 12);
        var month = ((absoluteMonth - 1) % 12) + 1;
        var day = Math.Min(start.Day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }
}
