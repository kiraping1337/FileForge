using FileForge.Application.Conversions;
using Microsoft.Extensions.Options;

namespace FileForge.Infrastructure.Conversion;

internal sealed class ImageMagickConverter(DockerSandbox sandbox, IOptions<ImageMagickOptions> options) : IFileConverter
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

    public Task ConvertAsync(
        ConversionRoute route,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken) =>
        sandbox.RunAsync(options.Value, BuildToolArguments(route), inputPath, outputPath, cancellationToken);

    /// <summary>
    /// Arguments for magick inside the container. The "png:" prefix forces the decoder:
    /// ImageMagick must not pick one by sniffing the content.
    /// </summary>
    internal static IReadOnlyList<string> BuildToolArguments(ConversionRoute route) =>
    [
        $"{route.SourceFormat}:{DockerSandbox.InputPath}",
        $"{route.TargetFormat}:{DockerSandbox.OutputPath}",
    ];
}
