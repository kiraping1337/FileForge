namespace FileForge.Application.Jobs;

public sealed class ConversionOptions
{
    public const string SectionName = "Conversions";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>How long files and the access token live after the job is created.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(1);
}
