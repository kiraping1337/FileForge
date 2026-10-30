using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class DeleteConversionService(IAppDbContext db, TimeProvider timeProvider, JobAccessChecker jobAccessChecker, JobFileCleaner fileCleaner)
{
    public async Task DeleteAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await jobAccessChecker.EnsureAccessAsync(now, id, token, cancellationToken);
        var job = await db.ConversionJobs
            .Include(j => j.Files)
            .Where(j => j.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job == null)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        if (job.Status == JobStatus.Processing)
        {
            throw new AppException(ErrorCodes.JobInProgress, "This job is in progress");
        }
        if (job.Status == JobStatus.Pending)
        {
            job.Cancel(now);
        }
        if (!fileCleaner.DeleteFiles(job))
        {
            throw new AppException(ErrorCodes.JobInProgress, "Files are in use, try again");
        }
        var tokens = await db.JobAccessTokens
            .Where(t => t.JobId == id)
            .ToListAsync(cancellationToken);
        db.JobAccessTokens.RemoveRange(tokens);
        await db.SaveChangesAsync(cancellationToken);
    }
}
