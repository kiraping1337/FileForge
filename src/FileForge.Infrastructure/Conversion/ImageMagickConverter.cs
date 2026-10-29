using System.Diagnostics;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileForge.Infrastructure.Conversion;

internal sealed class ImageMagickConverter(
    IOptions<ImageMagickOptions> options,
    ILogger<ImageMagickConverter> logger) : IFileConverter
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

    public async Task ConvertAsync(
        ConversionRoute route,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // No shell: arguments go to the program as separate strings, so nothing in them can be
        // interpreted as a command. They contain only server-made paths and formats from our list.
        var startInfo = new ProcessStartInfo(settings.ExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // The "png:" prefix forces the decoder: ImageMagick must not pick one by sniffing the content.
        startInfo.ArgumentList.Add($"{route.SourceFormat}:{inputPath}");
        startInfo.ArgumentList.Add($"{route.TargetFormat}:{outputPath}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("ImageMagick could not be started.");

        try
        {
            // Read stderr while waiting: if its buffer fills up, the process blocks forever.
            var errorOutput = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var stderr = await errorOutput;

            if (process.ExitCode != 0)
            {
                logger.LogWarning("ImageMagick exited with code {ExitCode}", process.ExitCode);
                logger.LogDebug("ImageMagick error output: {ErrorOutput}", stderr);
                throw new AppException(ErrorCodes.ProcessingFailed, "The file could not be converted.");
            }
        }
        catch (OperationCanceledException)
        {
            Kill(process);

            if (cancellationToken.IsCancellationRequested)
            {
                throw; // the worker is shutting down, not a timeout
            }

            throw new AppException(ErrorCodes.ProcessingTimeout, "The conversion took too long.");
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
