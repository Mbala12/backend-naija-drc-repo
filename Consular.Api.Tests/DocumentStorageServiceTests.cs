using Consular.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Consular.Api.Tests;

public class DocumentStorageServiceTests
{
    [Fact]
    public void SaveUpload_CreatesFileAndReturnsRelativePath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "consular-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new LocalDocumentStorageService(tempRoot, NullLogger<LocalDocumentStorageService>.Instance);
            var fileBytes = System.Text.Encoding.UTF8.GetBytes("hello world");
            using var stream = new MemoryStream(fileBytes);

            var result = service.SaveUpload("sample.pdf", stream);

            Assert.StartsWith("/uploads/", result);
            Assert.True(File.Exists(Path.Combine(tempRoot, result.TrimStart('/').Replace('/', Path.DirectorySeparatorChar).Replace("uploads" + Path.DirectorySeparatorChar, string.Empty))));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
