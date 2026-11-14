using System.Globalization;

namespace FinTrack.SharedKernel;

/// <summary>
/// Para değeri: tutar + ISO 4217 para birimi.
/// Kayan nokta (float/double) kullanılmaz; yuvarlama hatası kabul edilemez.
/// </summary>
/// <remarks>
/// Veritabanında <c>decimal(19,4)</c> olarak saklanır.
/// v1'de yalnızca TRY kullanılır; model çoklu birime açıktır.
/// <c>default(Money)</c> geçersizdir — her zaman <see cref="Of"/> veya <see cref="Try"/> kullanın.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    public const string DefaultCurrency = "TRY";
    public const int StorageScale = 4;

    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Türk lirası cinsinden para.</summary>
    public static Money Try(decimal amount) => Of(amount, DefaultCurrency);

    public static Money Of(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Para birimi ISO 4217 üç harfli kod olmalıdır.", nameof(currency));
        }

        return new Money(
            decimal.Round(amount, StorageScale, MidpointRounding.AwayFromZero),
            currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = DefaultCurrency) => Of(0m, currency);

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsPositive => Amount > 0m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    public Money Multiply(decimal factor) =>
        new(decimal.Round(Amount * factor, StorageScale, MidpointRounding.AwayFromZero), Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Para birimleri farklı: {Currency} ve {other.Currency}. Önce dönüştürme yapılmalıdır.");
        }
    }

    /// <summary>Kullanıcıya gösterilecek biçim: <c>1.954,62 TRY</c>.</summary>
    public string ToDisplayString() => $"{Amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} {Currency}";

    public override string ToString() =>
        $"{Amount.ToString("0.####", CultureInfo.InvariantCulture)} {Currency}";
}
