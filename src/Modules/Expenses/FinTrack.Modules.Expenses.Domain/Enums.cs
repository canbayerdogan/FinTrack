namespace FinTrack.Modules.Expenses.Domain;

public enum PaymentMethod
{
    Cash = 0,
    DebitCard = 1,
    CreditCard = 2,
    BankTransfer = 3,
    Other = 4,
}

public enum ExpenseStatus
{
    Active = 0,
    PartiallyRefunded = 1,
    Refunded = 2,
    Cancelled = 3,
}

public enum RefundStatus
{
    Active = 0,
    Cancelled = 1,
}
