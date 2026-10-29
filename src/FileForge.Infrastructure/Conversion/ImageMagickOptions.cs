namespace FileForge.Infrastructure.Conversion;

public sealed class ImageMagickOptions
{
    public const string SectionName = "ImageMagick";

    /// <summary>"magick" is found through PATH; set a full path if it is installed elsewhere.</summary>
    public string ExecutablePath { get; set; } = "magick";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
