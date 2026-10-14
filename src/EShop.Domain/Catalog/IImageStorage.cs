namespace EShop.Domain.Catalog;

public interface IImageStorage
{
    /// <summary>Saves the content under a new server-generated name and returns that name.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>Opens a stored file, or returns null if it does not exist.</summary>
    Stream? OpenRead(string fileName);

    /// <summary>Deletes a stored file. Does nothing if it does not exist.</summary>
    Task DeleteAsync(string fileName, CancellationToken cancellationToken);
}