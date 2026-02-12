namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for date/time operations.
/// </summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTime Today { get; }
}

