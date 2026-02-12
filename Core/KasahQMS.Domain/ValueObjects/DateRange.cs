using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.ValueObjects;

/// <summary>
/// Date range value object.
/// </summary>
public sealed class DateRange : ValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }
    
    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }
    
    public static DateRange Create(DateTime start, DateTime end)
    {
        if (end < start)
            throw new ArgumentException("End date cannot be before start date.");
        
        return new DateRange(start, end);
    }
    
    public bool Contains(DateTime date) => date >= Start && date <= End;
    
    public bool Overlaps(DateRange other) =>
        Start <= other.End && End >= other.Start;
    
    public TimeSpan Duration => End - Start;
    
    public int DaysCount => (int)Math.Ceiling(Duration.TotalDays);
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
    
    public override string ToString() => $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd}";
}
