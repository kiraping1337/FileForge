using FileForge.Application.Conversions;
using FileForge.Application.Errors;

namespace FileForge.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for ImageMagick/FFmpeg: integration tests check the API, the database and the queue,
/// not the converters (those are covered by their own tests and run in Docker).
/// "Converts" by copying the file, so a test can compare the download with what it uploaded.
/// </summary>
public sealed class CopyConverter : IFileConverter
{
    /// <summary>This route always fails, to test the Failed path.</summary>
    public static readonly ConversionRoute FailingRoute = new("png", "bmp");

    public string Name => "Copy (test)";

    public IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; } =
    [
        new("png", "jpg"),
        new("png", "webp"),
        FailingRoute,
    ];

    public Task ConvertAsync(ConversionRoute route, string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        if (route == FailingRoute)
        {
            throw new AppException(ErrorCodes.ProcessingFailed, "Simulated converter failure.");
        }

        File.Copy(inputPath, outputPath);
        return Task.CompletedTask;
    }
}
