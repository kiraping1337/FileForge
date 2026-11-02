using FileForge.Application.Abstractions;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using FileForge.Application.Storage;
using FileForge.Domain.Entities;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Jobs;

/// <summary>
/// Processes one job from the queue: reserve, convert, save the result or the error.
/// While converting, watches for a cancellation request from DELETE /conversions/{id}.
/// </summary>
public sealed class ConversionProcessor(
    IAppDbContext db,
    IJobQueue queue,
    IFileStorage storage,
    ConversionRegistry registry,
    JobFileCleaner fileCleaner,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ConversionProcessor> logger)
{
    private static readonly TimeSpan CancellationPollInterval = TimeSpan.FromSeconds(2);

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

        // Two different reasons to stop a conversion: the user cancelled the job (userCancellation)
        // or the worker is shutting down (cancellationToken). The converter stops on either.
        using var userCancellation = new CancellationTokenSource();
        using var conversion = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, userCancellation.Token);
        using var stopWatching = new CancellationTokenSource();
        var watcher = WatchForCancellationAsync(job.Id, userCancellation, stopWatching.Token);

        try
        {
            var converter = registry.GetConverter(job.SourceFormat, job.TargetFormat);

            await converter.ConvertAsync(
                new ConversionRoute(job.SourceFormat, job.TargetFormat),
                storage.GetLocalPath(original.StorageKey),
                storage.GetLocalPath(resultKey),
                conversion.Token);

            // A request that arrived just as the conversion finished still wins:
            // the user was told the job would be cancelled.
            if (await IsCancellationRequestedAsync(job.Id, CancellationToken.None))
            {
                CancelJob(job, resultKey);
            }
            else
            {
                job.Complete(resultKey, storage.GetSize(resultKey), Now());

                logger.LogInformation(
                    "Job {JobId} completed in {DurationMs} ms, {OutputSizeBytes} bytes",
                    job.Id, job.ProcessingDurationMs, job.OutputSizeBytes);
            }
        }
        catch (OperationCanceledException) when (userCancellation.IsCancellationRequested)
        {
            // The converter is already stopped (the sandbox killed the container).
            CancelJob(job, resultKey);
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
        finally
        {
            await stopWatching.CancelAsync();
            await watcher;
        }

        // Not the shutdown token: the outcome must be saved even if the worker is stopping right now.
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private void CancelJob(ConversionJob job, string resultKey)
    {
        storage.Delete(resultKey);
        job.Cancel(Now());

        // The original is not needed any more. If it is locked right now, the cleanup service retries.
        fileCleaner.DeleteFiles(job);

        logger.LogInformation("Job {JobId} cancelled by the user", job.Id);
    }

    /// <summary>
    /// Runs next to the conversion and checks the database every few seconds.
    /// Signals <paramref name="userCancellation"/> as soon as DELETE has requested cancellation.
    /// </summary>
    private async Task WatchForCancellationAsync(
        Guid jobId,
        CancellationTokenSource userCancellation,
        CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(CancellationPollInterval, timeProvider, stop);

                try
                {
                    if (await IsCancellationRequestedAsync(jobId, stop))
                    {
                        logger.LogInformation("Cancellation requested for job {JobId}, stopping the converter", jobId);
                        await userCancellation.CancelAsync();
                        return;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // E.g. the database is briefly unavailable: keep converting, check again next time.
                    logger.LogWarning(
                        "Could not check job {JobId} for cancellation ({ErrorType})", jobId, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The conversion is over, nothing to watch.
        }
    }

    private async Task<bool> IsCancellationRequestedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        // A separate DbContext: the processor's own context holds the job being converted,
        // and a DbContext must never be used by two tasks at the same time.
        await using var scope = scopeFactory.CreateAsyncScope();
        var freshDb = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await freshDb.ConversionJobs.AnyAsync(
            j => j.Id == jobId && j.CancellationRequestedAtUtc != null,
            cancellationToken);
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
