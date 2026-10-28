namespace FileForge.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Defaults to the OS temp folder: outside the repository and outside anything the web server serves.
    /// In Docker it will point to a mounted volume.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "FileForge", "storage");
}
