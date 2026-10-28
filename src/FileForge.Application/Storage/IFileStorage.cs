namespace FileForge.Application.Storage;

/// <summary>
/// Temporary file storage. Files are addressed only by server-generated keys,
/// never by user-supplied names or paths.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves the stream under a new random key. Throws FILE_TOO_LARGE as soon as
    /// more than <paramref name="maxBytes"/> are read; nothing is left behind in that case.
    /// </summary>
    Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken);

    Stream OpenRead(string storageKey);

    /// <summary>Does nothing if the file is already gone.</summary>
    void Delete(string storageKey);
}
