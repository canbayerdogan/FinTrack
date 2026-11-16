namespace FinTrack.Modules.CashInflows.Domain;

/// <summary>Para girişinin kaynağı (docs/05 §4.2).</summary>
public enum CashInflowSource
{
    Salary = 0,
    BusinessIncome = 1,
    BankLoan = 2,
    CashAdvance = 3,
    PersonalBorrowing = 4,
    AssetSale = 5,
    Unknown = 6,
}

/// <summary>Durum türetilir; ayrı bir durum alanı saklanmaz.</summary>
public enum CashInflowStatus
{
    Active = 0,
    Cancelled = 1,
}

/// <summary>Borç bağlantısının sahibi (docs/06 §5.3).</summary>
public enum DebtLinkType
{
    Loan = 0,
    CashAdvance = 1,
    PersonalDebt = 2,
}
