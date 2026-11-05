namespace FileForge.Application.Conversions;

public interface IFileConverter
{
    string Name { get; }

    IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; }

    /// <summary>
    /// Reads <paramref name="inputPath"/> and writes the result to <paramref name="outputPath"/>.
    /// Throws AppException with PROCESSING_FAILED or PROCESSING_TIMEOUT if the tool fails.
    /// </summary>
    Task ConvertAsync(ConversionRoute route, string inputPath, string outputPath, CancellationToken cancellationToken);
}
