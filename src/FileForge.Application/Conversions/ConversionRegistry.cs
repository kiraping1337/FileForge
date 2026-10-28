namespace FileForge.Application.Conversions;

/// <summary>
/// Collects the routes of all registered converters. The single source of truth for
/// "which conversions are allowed": both GET /formats and the upload validation use it.
/// </summary>
public sealed class ConversionRegistry
{
    private readonly Dictionary<ConversionRoute, IFileConverter> _converters = new();

    public ConversionRegistry(IEnumerable<IFileConverter> converters)
    {
        foreach (var converter in converters)
        {
            foreach (var route in converter.SupportedRoutes)
            {
                // Two tools claiming the same route is a configuration bug: fail at startup, not at runtime.
                if (!_converters.TryAdd(route, converter))
                {
                    throw new InvalidOperationException(
                        $"Conversion {route} is provided by both {_converters[route].Name} and {converter.Name}.");
                }
            }
        }

        // Built once: the set of converters does not change while the app is running.
        SupportedFormats = _converters.Keys
            .GroupBy(route => route.SourceFormat)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SupportedFormat(
                group.Key,
                group.Select(route => route.TargetFormat).Order(StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public IReadOnlyList<SupportedFormat> SupportedFormats { get; }

    public bool IsSupported(string? sourceFormat, string? targetFormat)
    {
        if (string.IsNullOrWhiteSpace(sourceFormat)
            || string.IsNullOrWhiteSpace(targetFormat)
            || string.Equals(sourceFormat, targetFormat, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _converters.ContainsKey(new ConversionRoute(sourceFormat, targetFormat));
    }
}
