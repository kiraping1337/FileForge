using FileForge.Application.Conversions;

namespace FileForge.Infrastructure.Conversion;

internal sealed class ImageMagickConverter : IFileConverter
{
    private static readonly string[] Formats = ["jpg", "png", "webp", "bmp"];

    public string Name => "ImageMagick";

    // Every format to every other format: 4 sources x 3 targets.
    public IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; } =
        Formats
            .SelectMany(source => Formats
                .Where(target => target != source)
                .Select(target => new ConversionRoute(source, target)))
            .ToArray();
}
