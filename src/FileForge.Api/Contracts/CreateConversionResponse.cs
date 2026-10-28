namespace FileForge.Api.Contracts;

public sealed record CreateConversionResponse(Guid JobId, string AccessToken, string Status, string StatusUrl);
