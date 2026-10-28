namespace FileForge.Application.Conversions;

public sealed record SupportedFormat(string Source, IReadOnlyList<string> Targets);
