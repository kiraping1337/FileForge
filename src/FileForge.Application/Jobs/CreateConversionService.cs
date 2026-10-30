using FileForge.Application.Abstractions;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using FileForge.Application.Storage;
using FileForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileForge.Application.Jobs;

public sealed class CreateConversionService(
    IAppDbContext db,
    IFileStorage storage,
    IFormatDetector formatDetector,
    ConversionRegistry registry,
    IOptions<ConversionOptions> options,
    TimeProvider timeProvider)
{
    public async Task<CreatedConversion> CreateAsync(
        Stream content,
        string? fileName,
        string targetFormat,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(targetFormat);

        var settings = options.Value;
        var target = targetFormat.Trim();

        // Soft limit: the incoming size is unknown yet, so assume the maximum. Parallel uploads
        // may overshoot the limit slightly, which is acceptable for temporary storage.
        var usedBytes = await db.ConversionFiles.SumAsync(f => f.FileSizeBytes, cancellationToken);
        if (usedBytes + settings.MaxFileSizeBytes > settings.MaxStorageBytes)
        {
            throw new AppException(ErrorCodes.StorageLimitExceeded, "The service is busy. Try again later.");
        }

        var stored = await storage.SaveAsync(content, settings.MaxFileSizeBytes, cancellationToken);

        // From here on the file exists on disk: any failure below must remove it,
        // otherwise it becomes an orphan that no job points to.
        try
        {
            var source = await DetectFormatAsync(stored.StorageKey, cancellationToken)
                ?? throw new AppException(
                    ErrorCodes.UnsupportedFormat,
                    "The file format is not recognized or not supported.");

            if (!registry.IsSupported(source, target))
            {
                // The requested format is not echoed back: it is raw user input.
                throw new AppException(
                    ErrorCodes.ConversionNotSupported,
                    $"Conversion from {source} to the requested format is not supported.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;

            var job = ConversionJob.Create(source, target, SanitizeFileName(fileName), stored.SizeBytes, now, settings.Retention);
            job.AddOriginalFile(stored.StorageKey, source, now);

            var (token, tokenHash) = AccessTokens.Generate();

            db.ConversionJobs.Add(job);
            db.JobAccessTokens.Add(JobAccessToken.Create(job.Id, tokenHash, now, job.ExpiresAtUtc));

            // Job, file metadata and token are saved in one transaction: all or nothing.
            await db.SaveChangesAsync(cancellationToken);

            return new CreatedConversion(job.Id, token, job.Status);
        }
        catch
        {
            storage.Delete(stored.StorageKey);
            throw;
        }
    }

    private async Task<string?> DetectFormatAsync(string storageKey, CancellationToken cancellationToken)
    {
        await using var saved = storage.OpenRead(storageKey);
        return await formatDetector.DetectAsync(saved, cancellationToken);
    }

    /// <summary>
    /// The name is only shown back to the user, never used as a path. Still: drop any directory part
    /// (some clients send "C:\Users\...\photo.png"), control characters and excess length.
    /// </summary>
    private static string SanitizeFileName(string? fileName)
    {
        var name = fileName ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (name.Length == 0)
        {
            return "file";
        }

        return name.Length <= ConversionJob.MaxFileNameLength
            ? name
            : name[..ConversionJob.MaxFileNameLength];
    }
}
