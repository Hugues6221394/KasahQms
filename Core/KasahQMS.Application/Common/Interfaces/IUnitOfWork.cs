namespace KasahQMS.Application.Common.Interfaces;

/// <summary>
/// Unit of Work interface for transaction management.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

