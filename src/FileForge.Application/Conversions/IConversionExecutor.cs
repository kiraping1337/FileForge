namespace FileForge.Application.Conversions;

/// <summary>
/// Converts one stored file into another stored file. Two implementations:
/// locally (the converter runs the sandbox itself) or remotely (the sandbox runner service does it,
/// so the worker never needs access to Docker).
/// </summary>
public interface IConversionExecutor
{
    /// <summary>Throws AppException with PROCESSING_FAILED, PROCESSING_TIMEOUT or CONVERSION_NOT_SUPPORTED.</summary>
    Task ExecuteAsync(ConversionRoute route, string inputStorageKey, string outputStorageKey, CancellationToken cancellationToken);
}
