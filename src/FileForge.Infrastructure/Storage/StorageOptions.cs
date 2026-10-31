namespace FileForge.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Outside the repository and outside anything the web server serves. LocalApplicationData
    /// instead of the temp folder: on Windows the temp path may contain short 8.3 names
    /// (C:\Users\ABCD~1\...), which Docker cannot mount into the converter container.
    /// In Docker this will point to a mounted volume.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileForge", "storage");
}
