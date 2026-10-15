using System.Text.RegularExpressions;
using EShop.Domain.Catalog;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Images;

public sealed partial class LocalImageStorage : IImageStorage
{
    private readonly string _root;

    public LocalImageStorage(IOptions<ImageStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        Stream content, string extension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(extension);

        var ext = extension.ToLowerInvariant();
        if (!ExtensionPattern().IsMatch(ext))
        {
            throw new ArgumentException("Invalid file extension.", nameof(extension));
        }

        var fileName = $"{Guid.CreateVersion7():N}{ext}";
        var path = Path.Combine(_root, fileName);

        try
        {
            await using var file = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            // Do not leave a partial file behind.
            File.Delete(path);
            throw;
        }

        return fileName;
    }

    public Stream? OpenRead(string fileName)
    {
        var path = ResolvePath(fileName);
        return path is not null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public Task DeleteAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(fileName);
        if (path is not null)
        {
            File.Delete(path); // no exception if the file does not exist
        }

        return Task.CompletedTask;
    }

    // Only plain file names are accepted, so a name can never leave the root folder.
    private string? ResolvePath(string fileName)
        => string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName
            ? null
            : Path.Combine(_root, fileName);

    [GeneratedRegex(@"^\.[a-z0-9]{1,5}$")]
    private static partial Regex ExtensionPattern();
}