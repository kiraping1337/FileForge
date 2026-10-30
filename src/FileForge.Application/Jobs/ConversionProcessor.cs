using FileForge.Application.Abstractions;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using FileForge.Application.Storage;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Jobs;

/// <summary>
/// Processes one job from the queue: reserve, convert, save the result or the error.
/// </summary>
public sealed class ConversionProcessor(
    IAppDbContext db,
    IJobQueue queue,
    IFileStorage storage,
    ConversionRegistry registry,
    TimeProvider timeProvider,
    ILogger<ConversionProcessor> logger)
{
    /// <returns>false if the queue was empty.</returns>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var jobId = await queue.TryReserveNextAsync(cancellationToken);
        if (jobId is null)
        {
            return false;
        }

        var job = await db.ConversionJobs
            .Include(j => j.Files)
            .FirstAsync(j => j.Id == jobId.Value, cancellationToken);

        logger.LogInformation(
            "Processing job {JobId}: {SourceFormat} -> {TargetFormat}, {FileSizeBytes} bytes",
            job.Id, job.SourceFormat, job.TargetFormat, job.FileSizeBytes);

        var original = job.Files.Single(f => f.Role == FileRole.Original);
        var resultKey = storage.CreateKey();

        try
        {
            var converter = registry.GetConverter(job.SourceFormat, job.TargetFormat);

            await converter.ConvertAsync(
                new ConversionRoute(job.SourceFormat, job.TargetFormat),
                storage.GetLocalPath(original.StorageKey),
                storage.GetLocalPath(resultKey),
                cancellationToken);

            job.Complete(resultKey, storage.GetSize(resultKey), Now());

            logger.LogInformation(
                "Job {JobId} completed in {DurationMs} ms, {OutputSizeBytes} bytes",
                job.Id, job.ProcessingDurationMs, job.OutputSizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The worker is shutting down normally: give the job back right away instead of
            // leaving it to the stuck-job recovery.
            storage.Delete(resultKey);
            job.ReturnToQueue();
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("Job {JobId} returned to the queue because the worker is stopping", job.Id);
            throw;
        }
        catch (AppException ex)
        {
            storage.Delete(resultKey);
            logger.LogWarning("Job {JobId} failed with {ErrorCode}", job.Id, ex.Code);
            job.Fail(ex.Code, Now());
        }
        catch (Exception ex)
        {
            storage.Delete(resultKey);
            logger.LogError(ex, "Job {JobId} failed unexpectedly", job.Id);
            job.Fail(ErrorCodes.ProcessingFailed, Now());
        }

        // Not the shutdown token: the outcome must be saved even if the worker is stopping right now.
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
