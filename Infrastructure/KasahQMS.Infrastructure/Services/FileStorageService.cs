using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// File storage service implementation with path traversal protection.
/// Supports local file system storage.
/// Can be extended for cloud storage (Azure Blob, AWS S3, etc.)
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _basePath;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _basePath = Path.GetFullPath(
            configuration["FileStorage:BasePath"] ?? Path.Combine(AppContext.BaseDirectory, "Storage"));

        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid tenantId,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate unique file name to prevent collisions
            var uniqueFileName = $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}";

            // Build path with tenant isolation
            var relativePath = Path.Combine(
                tenantId.ToString(),
                folder ?? "general",
                DateTime.UtcNow.ToString("yyyy/MM"));

            var fullPath = Path.Combine(_basePath, relativePath);

            // Ensure directory exists
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            var filePath = Path.Combine(fullPath, uniqueFileName);

            // Validate path is within base path (prevent path traversal)
            ValidatePathWithinBase(filePath);

            // Write file
            using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await fileStream.CopyToAsync(outputStream, cancellationToken);

            var storagePath = Path.Combine(relativePath, uniqueFileName);

            _logger.LogInformation(
                "File uploaded successfully: {FileName} -> {StoragePath}",
                fileName, storagePath);

            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream?> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFullPath(storagePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found: {StoragePath}", storagePath);
                return null;
            }

            var memoryStream = new MemoryStream();
            using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Path traversal attempt detected: {StoragePath}", storagePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {StoragePath}", storagePath);
            throw;
        }
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFullPath(storagePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found for deletion: {StoragePath}", storagePath);
                return Task.FromResult(false);
            }

            File.Delete(fullPath);

            _logger.LogInformation("File deleted: {StoragePath}", storagePath);
            return Task.FromResult(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Path traversal attempt detected: {StoragePath}", storagePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {StoragePath}", storagePath);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFullPath(storagePath);
            return Task.FromResult(File.Exists(fullPath));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<long> GetFileSizeAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFullPath(storagePath);

            if (!File.Exists(fullPath))
            {
                return Task.FromResult(0L);
            }

            var fileInfo = new FileInfo(fullPath);
            return Task.FromResult(fileInfo.Length);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(0L);
        }
    }

    public async Task<string> CopyAsync(
        string sourceStoragePath,
        Guid targetTenantId,
        string? targetFolder = null,
        CancellationToken cancellationToken = default)
    {
        var sourceStream = await DownloadAsync(sourceStoragePath, cancellationToken);
        if (sourceStream == null)
        {
            throw new FileNotFoundException("Source file not found", sourceStoragePath);
        }

        var fileName = Path.GetFileName(sourceStoragePath);
        return await UploadAsync(sourceStream, fileName, "application/octet-stream", targetTenantId, targetFolder, cancellationToken);
    }

    /// <summary>
    /// Gets the full path and validates it is within the base path to prevent path traversal attacks.
    /// </summary>
    private string GetSafeFullPath(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, storagePath));
        ValidatePathWithinBase(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Validates that the given path is within the base storage path.
    /// Throws UnauthorizedAccessException if path traversal is detected.
    /// </summary>
    private void ValidatePathWithinBase(string fullPath)
    {
        var normalizedPath = Path.GetFullPath(fullPath);
        var normalizedBase = Path.GetFullPath(_basePath);

        if (!normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Path traversal attempt blocked. Path: {Path}, Base: {Base}",
                normalizedPath, normalizedBase);
            throw new UnauthorizedAccessException("Access to path outside storage directory is not allowed.");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limit length
        if (sanitized.Length > 100)
        {
            var extension = Path.GetExtension(sanitized);
            var name = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = name[..Math.Min(name.Length, 90)] + extension;
        }

        return sanitized;
    }
}
