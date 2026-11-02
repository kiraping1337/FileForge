using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class DeleteConversionService(IAppDbContext db, TimeProvider timeProvider, JobAccessChecker jobAccessChecker, JobFileCleaner fileCleaner)
{
    /// <summary>
    /// Cancels the job and deletes its files and tokens.
    /// A job that is being converted cannot be deleted right away: only a cancellation
    /// request is saved, and the worker finishes it within a few seconds.
    /// </summary>
    /// <returns>true if only cancellation was requested, false if everything was deleted.</returns>
    public async Task<bool> DeleteAsync(Guid id, string? token, CancellationToken cancellationToken)
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
            job.RequestCancellation(now);
            await db.SaveChangesAsync(cancellationToken);
            return true;
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
        return false;
    }
}
