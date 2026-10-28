namespace FileForge.Application.Jobs;

public sealed record ConversionDetails(Guid JobId, string OriginalFileName, string SourceFormat, string TargetFormat,
                                           string Status, long? OutputSizeBytes, string? ErrorCode, DateTime CreatedAtUtc,
                                           DateTime ExpiresAtUtc, bool DownloadAvailable);

