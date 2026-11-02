using FileForge.Domain.Enums;
using FileForge.Domain.Exceptions;

namespace FileForge.Domain.Entities;

/// <summary>
/// One conversion request: a single source file converted to a single target format.
/// Properties have private setters, so the status can only change through the methods
/// below. That makes invalid transitions (e.g. Pending -> Completed) impossible.
/// </summary>
public class ConversionJob
{
    public const int MaxFileNameLength = 255;
    public const int MaxFormatLength = 16;
    public const int MaxErrorCodeLength = 64;

    private readonly List<ConversionFile> _files = [];

    // EF Core needs a parameterless constructor to create objects when reading from the database.
    // It is private so application code has to use Create().
    private ConversionJob()
    {
    }

    public Guid Id { get; private set; }

    public JobStatus Status { get; private set; }

    public string SourceFormat { get; private set; } = null!;

    public string TargetFormat { get; private set; } = null!;

    /// <summary>Display name only. Never used as a storage path.</summary>
    public string OriginalFileName { get; private set; } = null!;

    public long FileSizeBytes { get; private set; }

    public long? OutputSizeBytes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Safe error code such as PROCESSING_TIMEOUT. Never an exception message.</summary>
    public string? ErrorCode { get; private set; }

    public long? ProcessingDurationMs { get; private set; }

    /// <summary>How many times a worker has picked up this job. Used to cap retries.</summary>
    public int AttemptCount { get; private set; }
    /// <summary>Set when the user asked to cancel a job that is already being converted.</summary>
    public DateTime? CancellationRequestedAtUtc { get; private set; }

    public IReadOnlyCollection<ConversionFile> Files => _files;

    public static ConversionJob Create(
        string sourceFormat,
        string targetFormat,
        string originalFileName,
        long fileSizeBytes,
        DateTime nowUtc,
        TimeSpan retention)
    {
        Guard.RequireText(sourceFormat, MaxFormatLength, nameof(sourceFormat));
        Guard.RequireText(targetFormat, MaxFormatLength, nameof(targetFormat));
        Guard.RequireText(originalFileName, MaxFileNameLength, nameof(originalFileName));
        Guard.RequireNonNegative(fileSizeBytes, nameof(fileSizeBytes));
        Guard.RequireUtc(nowUtc, nameof(nowUtc));

        if (retention <= TimeSpan.Zero)
        {
            throw new DomainException("Retention period must be positive.");
        }

        var source = sourceFormat.ToLowerInvariant();
        var target = targetFormat.ToLowerInvariant();

        if (source == target)
        {
            throw new DomainException("Source and target formats must differ.");
        }

        return new ConversionJob
        {
            // Version 7 GUIDs are time-ordered, which keeps the primary key index compact.
            Id = Guid.CreateVersion7(),
            Status = JobStatus.Pending,
            SourceFormat = source,
            TargetFormat = target,
            OriginalFileName = originalFileName,
            FileSizeBytes = fileSizeBytes,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + retention,
        };
    }

    public ConversionFile AddOriginalFile(string storageKey, string detectedFormat, DateTime nowUtc)
    {
        EnsureStatus(nameof(AddOriginalFile), JobStatus.Pending);

        if (_files.Any(f => f.Role == FileRole.Original))
        {
            throw new DomainException("The original file is already attached.");
        }

        var file = ConversionFile.Create(Id, FileRole.Original, storageKey, FileSizeBytes, detectedFormat, nowUtc);
        _files.Add(file);
        return file;
    }

    /// <summary>Pending -> Processing. Called by the worker after it reserves the job.</summary>
    public void StartProcessing(DateTime nowUtc)
    {
        Guard.RequireUtc(nowUtc, nameof(nowUtc));
        EnsureStatus(nameof(StartProcessing), JobStatus.Pending);

        Status = JobStatus.Processing;
        StartedAtUtc = nowUtc;
        AttemptCount++;
    }

    /// <summary>Processing -> Completed. Records the result file.</summary>
    public ConversionFile Complete(string resultStorageKey, long outputSizeBytes, DateTime nowUtc)
    {
        Guard.RequireUtc(nowUtc, nameof(nowUtc));
        EnsureStatus(nameof(Complete), JobStatus.Processing);

        var result = ConversionFile.Create(Id, FileRole.Result, resultStorageKey, outputSizeBytes, TargetFormat, nowUtc);
        _files.Add(result);

        Status = JobStatus.Completed;
        OutputSizeBytes = outputSizeBytes;
        CompletedAtUtc = nowUtc;
        ProcessingDurationMs = MillisecondsSinceStart(nowUtc);
        ErrorCode = null;
        return result;
    }

    /// <summary>Pending or Processing -> Failed.</summary>
    public void Fail(string errorCode, DateTime nowUtc)
    {
        Guard.RequireText(errorCode, MaxErrorCodeLength, nameof(errorCode));
        Guard.RequireUtc(nowUtc, nameof(nowUtc));
        EnsureStatus(nameof(Fail), JobStatus.Pending, JobStatus.Processing);

        Status = JobStatus.Failed;
        ErrorCode = errorCode;
        CompletedAtUtc = nowUtc;
        ProcessingDurationMs = MillisecondsSinceStart(nowUtc);
    }

    /// <summary>
    /// Processing -> Pending. Used when a worker crashed and the job was found stuck.
    /// AttemptCount is kept, so the caller can decide to fail the job instead of retrying forever.
    /// </summary>
    public void ReturnToQueue()
    {
        EnsureStatus(nameof(ReturnToQueue), JobStatus.Processing);

        Status = JobStatus.Pending;
        StartedAtUtc = null;
    }

    /// <summary>Pending or Processing -> Cancelled.</summary>
    public void Cancel(DateTime nowUtc)
    {
        Guard.RequireUtc(nowUtc, nameof(nowUtc));
        EnsureStatus(nameof(Cancel), JobStatus.Pending, JobStatus.Processing);

        Status = JobStatus.Cancelled;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>
    /// Processing -> still Processing, with a cancellation request. The worker sees the request,
    /// stops the converter and then calls Cancel(). Repeated calls keep the first request time.
    /// </summary>
    public void RequestCancellation(DateTime nowUtc)
    {
        Guard.RequireUtc(nowUtc, nameof (nowUtc));
        EnsureStatus(nameof(RequestCancellation), JobStatus.Processing);
        if (CancellationRequestedAtUtc == null) { 
            CancellationRequestedAtUtc = nowUtc;
        }
    }

    /// <summary>Any finished or waiting job -> Expired. Called by the cleanup service.</summary>
    public void Expire()
    {
        EnsureStatus(nameof(Expire), JobStatus.Pending, JobStatus.Completed, JobStatus.Failed, JobStatus.Cancelled);

        Status = JobStatus.Expired;
    }

    public bool IsExpiredAt(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    private void EnsureStatus(string action, params JobStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidJobStateException(Id, Status, action);
        }
    }

    private long? MillisecondsSinceStart(DateTime nowUtc) =>
        StartedAtUtc is { } started
            ? (long)(nowUtc - started).TotalMilliseconds
            : null;
}
