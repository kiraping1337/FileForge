namespace FileForge.Application.Conversions;

/// <summary>
/// Determines the real format from the file content. The file name and the extension are never trusted.
/// </summary>
public interface IFormatDetector
{
    /// <returns>A format name such as "png", or null if the content is not recognized.</returns>
    Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken);
}
