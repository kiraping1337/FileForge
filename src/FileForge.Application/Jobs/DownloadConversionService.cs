using FileForge.Application.Abstractions;
using FileForge.Application.Errors;
using FileForge.Application.Storage;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Jobs;

public sealed class DownloadConversionService(IAppDbContext db, TimeProvider timeProvider, JobAccessChecker jobAccessChecker, IFileStorage fileStorage)
{
    public async Task<ConversionDownload> GetAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await jobAccessChecker.EnsureAccessAsync(now, id, token, cancellationToken);
        var job = await db.ConversionJobs
            .AsNoTracking()
            .Include(j => j.Files)
            .Where(j => j.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job == null)
        {
            throw new AppException(ErrorCodes.AccessDenied, "Access to this job is denied.");
        }
        if (job.Status != JobStatus.Completed)
        {
            throw new AppException(ErrorCodes.JobNotCompleted, "This job isn't completed");
        }
        var file = job.Files.First(f => f.Role == FileRole.Result);
        return new ConversionDownload(fileStorage.OpenRead(file.StorageKey), Path.ChangeExtension(job.OriginalFileName, job.TargetFormat));
    }
}
