using KasahQMS.Application.Common.Interfaces.Services;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// DateTime service implementation.
/// </summary>
public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today => DateTime.UtcNow.Date;
}
