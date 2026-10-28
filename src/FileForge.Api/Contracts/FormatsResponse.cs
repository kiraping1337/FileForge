namespace FileForge.Api.Contracts;

public sealed record FormatsResponse(IReadOnlyList<FormatResponse> Formats);

public sealed record FormatResponse(string Source, IReadOnlyList<string> Targets);
