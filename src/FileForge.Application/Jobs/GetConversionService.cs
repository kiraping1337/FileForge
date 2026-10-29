using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class GetConversionService(IAppDbContext db, TimeProvider timeProvider, JobAccessChecker jobAccessChecker)
{
    public async Task<ConversionDetails> GetAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await jobAccessChecker.EnsureAccessAsync(now, id, token, cancellationToken);
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
