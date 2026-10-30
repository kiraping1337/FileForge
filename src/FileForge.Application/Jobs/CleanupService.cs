using FileForge.Application.Abstractions;
using FileForge.Application.Storage;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileForge.Application.Jobs;

/// <summary>
/// One cleanup run: files of finished and expired jobs, expired tokens, orphan files.
/// </summary>
public sealed class CleanupService(
    IAppDbContext db,
    IFileStorage storage,
    JobFileCleaner fileCleaner,
    IOptions<CleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<CleanupService> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await CleanJobsAsync(now, cancellationToken);
        await DeleteExpiredTokensAsync(now, cancellationToken);
        await DeleteOrphanFilesAsync(now, cancellationToken);
    }

    private async Task CleanJobsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var batchSize = options.Value.BatchSize;

        // Processing jobs are never touched: the worker may be writing their result right now.
        // Selected: jobs that still have files and no longer need them (expired, failed or cancelled),
        // and expired jobs that are not marked Expired yet.
        var jobs = await db.ConversionJobs
            .Include(j => j.Files)
            .Where(j => j.Status != JobStatus.Processing
                && ((j.Files.Any() && (j.ExpiresAtUtc <= now
                                       || j.Status == JobStatus.Failed
                                       || j.Status == JobStatus.Cancelled))
                    || (j.ExpiresAtUtc <= now && j.Status != JobStatus.Expired)))
            .OrderBy(j => j.ExpiresAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return;
        }

        var expired = 0;
        foreach (var job in jobs)
        {
            var noFilesLeft = fileCleaner.DeleteFiles(job);

            if (noFilesLeft && job.IsExpiredAt(now) && job.Status != JobStatus.Expired)
            {
                job.Expire();
                expired++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cleanup: {JobCount} jobs cleaned, {ExpiredCount} marked expired", jobs.Count, expired);
    }

    private async Task DeleteExpiredTokensAsync(DateTime now, CancellationToken cancellationToken)
    {
        var tokens = await db.JobAccessTokens
            .Where(t => t.ExpiresAtUtc <= now)
            .OrderBy(t => t.ExpiresAtUtc)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return;
        }

        db.JobAccessTokens.RemoveRange(tokens);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cleanup: {TokenCount} expired tokens deleted", tokens.Count);
    }

    private async Task DeleteOrphanFilesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var olderThan = now - options.Value.OrphanFileMinAge;

        var candidates = storage.ListFiles()
            .Where(f => f.LastWriteUtc < olderThan)
            .Select(f => f.StorageKey)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var known = await db.ConversionFiles
            .Where(f => candidates.Contains(f.StorageKey))
            .Select(f => f.StorageKey)
            .ToListAsync(cancellationToken);

        var deleted = 0;
        foreach (var key in candidates.Except(known))
        {
            try
            {
                storage.Delete(key);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Could not delete an orphan file ({ErrorType}), will retry", ex.GetType().Name);
            }
        }

        if (deleted > 0)
        {
            logger.LogWarning("Cleanup: {FileCount} orphan files deleted", deleted);
        }
    }
}
