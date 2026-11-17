namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>Kart durumu saklanmaz; <c>IsArchived</c> alanından türetilir (docs/06 §6.2).</summary>
public enum CardStatus
{
    Active = 0,
    Archived = 1,
}

/// <summary>Kart ödemesi durumu saklanmaz; <c>CancelledAtUtc</c> alanından türetilir (docs/06 §6.4).</summary>
public enum PaymentStatus
{
    Active = 0,
    Cancelled = 1,
}

/// <summary>Ekstre durumu saklanmaz; <c>CancelledAtUtc</c> alanından türetilir (docs/06 §6.6).</summary>
public enum StatementStatus
{
    Active = 0,
    Cancelled = 1,
}

/// <summary>Nakit avans durumu saklanmaz; <c>CancelledAtUtc</c> alanından türetilir.</summary>
public enum CashAdvanceStatus
{
    Active = 0,
    Cancelled = 1,
}
