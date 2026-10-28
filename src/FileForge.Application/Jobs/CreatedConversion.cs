using FileForge.Domain.Enums;

namespace FileForge.Application.Jobs;

/// <summary>Result of creating a job. AccessToken is the plain token: it exists only here, never in the database.</summary>
public sealed record CreatedConversion(Guid JobId, string AccessToken, JobStatus Status);
