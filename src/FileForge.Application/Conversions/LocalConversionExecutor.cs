using FileForge.Application.Storage;

namespace FileForge.Application.Conversions;

/// <summary>Runs the converter in this process. Used by the sandbox runner, and by the worker in local development.</summary>
public sealed class LocalConversionExecutor(ConversionRegistry registry, IFileStorage storage) : IConversionExecutor
{
    public Task ExecuteAsync(
        ConversionRoute route,
        string inputStorageKey,
        string outputStorageKey,
        CancellationToken cancellationToken)
    {
        var converter = registry.GetConverter(route.SourceFormat, route.TargetFormat);

        return converter.ConvertAsync(
            route,
            storage.GetLocalPath(inputStorageKey),
            storage.GetLocalPath(outputStorageKey),
            cancellationToken);
    }
}
