namespace FileForge.Api.Contracts;

public sealed record GetConversionResponse(Guid JobId, string OriginalFileName, string SourceFormat, string TargetFormat, 
                                           string Status, long? OutputSizeBytes, string? ErrorCode, DateTime CreatedAtUtc, 
                                           DateTime ExpiresAtUtc, bool DownloadAvailable);

