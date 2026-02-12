namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for file storage operations.
/// </summary>
public interface IFileStorageService
{
    Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid tenantId,
        string? folder = null,
        CancellationToken cancellationToken = default);
    
    Task<Stream?> DownloadAsync(string storagePath, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default);
    
    Task<long> GetFileSizeAsync(string storagePath, CancellationToken cancellationToken = default);
    
    Task<string> CopyAsync(
        string sourceStoragePath,
        Guid targetTenantId,
        string? targetFolder = null,
        CancellationToken cancellationToken = default);
}
