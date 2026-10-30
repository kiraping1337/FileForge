using FileForge.Application.Abstractions;
using FileForge.Application.Storage;
using FileForge.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Jobs;

/// <summary>
/// Deletes the files of one job: first the physical file, then its row.
/// Used by the cleanup service and by DELETE /conversions/{id}.
/// </summary>
public sealed class JobFileCleaner(IAppDbContext db, IFileStorage storage, ILogger<JobFileCleaner> logger)
{
    /// <summary>
    /// The job must be loaded with Include(j => j.Files) and tracked. Does not save: the caller
    /// calls SaveChangesAsync. A file that cannot be deleted right now keeps its row, so the next
    /// cleanup run tries again.
    /// </summary>
    /// <returns>true if the job has no files left.</returns>
    public bool DeleteFiles(ConversionJob job)
    {
        var allDeleted = true;

        foreach (var file in job.Files.ToList())
        {
            try
            {
                storage.Delete(file.StorageKey);
                db.ConversionFiles.Remove(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Only the exception type: its message contains the full path.
                logger.LogWarning(
                    "Could not delete a file of job {JobId} ({ErrorType}), will retry",
                    job.Id, ex.GetType().Name);
                allDeleted = false;
            }
        }

        return allDeleted;
    }
}
