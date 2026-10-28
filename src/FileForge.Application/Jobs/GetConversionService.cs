using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class GetConversionService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<ConversionDetails> GetAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        byte[]? hash = AccessTokens.TryHash(token);
        if (hash == null) {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        var exists = await db.JobAccessTokens.AnyAsync(
            t => t.JobId == id && t.TokenHash == hash && t.ExpiresAtUtc > now,
            cancellationToken);
      
        if (!exists)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        var job = await db.ConversionJobs
            .AsNoTracking()
            .Where(j => j.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job == null)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        var isDownloadAvailable = job.Status == JobStatus.Completed && !job.IsExpiredAt(now);
        return new ConversionDetails(job.Id, job.OriginalFileName, job.SourceFormat, job.TargetFormat, 
                                     job.Status.ToString(), job.OutputSizeBytes, job.ErrorCode, job.CreatedAtUtc, job.ExpiresAtUtc, isDownloadAvailable);
    }
}
