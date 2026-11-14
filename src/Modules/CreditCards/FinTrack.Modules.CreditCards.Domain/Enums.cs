namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>Kart durumu saklanmaz; <c>IsArchived</c> alanından türetilir (docs/06 §6.2).</summary>
public enum CardStatus
{
    Active = 0,
    Archived = 1,
}
