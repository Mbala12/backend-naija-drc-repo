using Microsoft.Extensions.Hosting;

namespace Consular.Api.Services;

public interface IDocumentStorageService
{
    string SaveUpload(string fileName, Stream content);
}

public class LocalDocumentStorageService : IDocumentStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<LocalDocumentStorageService> _logger;

    public LocalDocumentStorageService(IHostEnvironment environment, ILogger<LocalDocumentStorageService> logger)
        : this(Path.Combine(environment.ContentRootPath, "uploads"), logger)
    {
    }

    public LocalDocumentStorageService(string rootPath, ILogger<LocalDocumentStorageService> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
        Directory.CreateDirectory(_rootPath);
    }

    public string SaveUpload(string fileName, Stream content)
    {
        var safeName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = Path.Combine(_rootPath, storedName);

        try
        {
            using (var stream = File.Create(targetPath))
            {
                content.CopyTo(stream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save upload {OriginalFileName} to {TargetPath}", fileName, targetPath);
            throw;
        }

        _logger.LogInformation("Saved upload {OriginalFileName} as {StoredName} ({Size} byte(s))", fileName, storedName, content.Length);
        return $"/uploads/{storedName}";
    }
}
