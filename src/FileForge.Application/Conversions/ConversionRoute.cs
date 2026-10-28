namespace FileForge.Application.Conversions;

/// <summary>
/// One allowed conversion, e.g. png -> jpg. Formats are normalized to lower case,
/// so "PNG -> JPG" and "png -> jpg" are equal routes.
/// </summary>
public sealed record ConversionRoute
{
    public ConversionRoute(string sourceFormat, string targetFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFormat);

        SourceFormat = sourceFormat.ToLowerInvariant();
        TargetFormat = targetFormat.ToLowerInvariant();

        if (SourceFormat == TargetFormat)
        {
            throw new ArgumentException("Source and target formats must differ.");
        }
    }

    public string SourceFormat { get; }

    public string TargetFormat { get; }

    public override string ToString() => $"{SourceFormat} -> {TargetFormat}";
}
