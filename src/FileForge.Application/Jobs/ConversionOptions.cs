namespace FileForge.Application.Jobs;

public sealed class ConversionOptions
{
    public const string SectionName = "Conversions";

    /// <summary>One limit for all formats: big enough for a short video, images are limited by ImageMagick's policy.</summary>
    public long MaxFileSizeBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>Total size of all stored files (originals and results). New uploads are refused above it.</summary>
    public long MaxStorageBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>How long files and the access token live after the job is created.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(1);
}
