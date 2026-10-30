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

    /// <summary>A new random key for a file that an external tool is about to write.</summary>
    string CreateKey();

    /// <summary>Path on disk for external tools such as ImageMagick, which work with files, not streams.</summary>
    string GetLocalPath(string storageKey);

    long GetSize(string storageKey);

    Stream OpenRead(string storageKey);

    /// <summary>
    /// Does nothing if the file is already gone. Throws IOException if the file is in use,
    /// e.g. being downloaded right now.
    /// </summary>
    void Delete(string storageKey);

    /// <summary>All files in the storage. Used to find orphans: files with no row in the database.</summary>
    IEnumerable<StoredFileInfo> ListFiles();
}
