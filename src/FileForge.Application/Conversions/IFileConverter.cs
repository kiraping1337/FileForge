namespace FileForge.Application.Conversions;

/// <summary>
/// An external tool that converts files (ImageMagick, FFmpeg).
/// The conversion method itself is added together with the Worker.
/// </summary>
public interface IFileConverter
{
    string Name { get; }

    IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; }
}
