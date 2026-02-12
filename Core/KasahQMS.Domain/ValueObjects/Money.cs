using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.ValueObjects;

/// <summary>
/// Money value object with currency support.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
    
    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.");
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.");
        
        currency = currency.Trim().ToUpperInvariant();
        
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.");
        
        return new Money(Math.Round(amount, 2), currency);
    }
    
    public static Money Zero(string currency = "USD") => new(0, currency);
    
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }
    
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var newAmount = Amount - other.Amount;
        if (newAmount < 0)
            throw new InvalidOperationException("Result cannot be negative.");
        return new Money(newAmount, Currency);
    }
    
    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot perform operation on different currencies.");
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public override string ToString() => $"{Amount:N2} {Currency}";
}
