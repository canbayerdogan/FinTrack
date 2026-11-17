namespace FinTrack.Modules.Debts.Domain;

/// <summary>Kredi yaşam döngüsü (docs/05 §6.7).</summary>
public enum LoanStatus
{
    Active = 0,
    Closed = 1,
    Cancelled = 2,
}

/// <summary>Taksit durumu (docs/05 §6.2).</summary>
public enum InstallmentStatus
{
    Planned = 0,
    Paid = 1,
    Cancelled = 2,
}

/// <summary>Ödeme/geri ödeme kaydının durumu. İptal hard delete değildir.</summary>
public enum DebtPaymentStatus
{
    Active = 0,
    Cancelled = 1,
}

/// <summary>Kişisel borç yaşam döngüsü (docs/05 §6.7).</summary>
public enum PersonalDebtStatus
{
    Active = 0,
    Closed = 1,
    Cancelled = 2,
}
