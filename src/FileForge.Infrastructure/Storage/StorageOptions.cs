namespace FileForge.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// LocalApplicationData, not the temp folder: Windows temp paths can contain 8.3 short names
    /// (C:\Users\ABCD~1\...), which Docker cannot mount. In Docker this is a mounted volume.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileForge", "storage");
}
