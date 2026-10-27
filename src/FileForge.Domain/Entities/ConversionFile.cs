using FileForge.Domain.Enums;

namespace FileForge.Domain.Entities;

/// <summary>
/// Metadata of a file stored in temporary storage: either the uploaded original or the result.
/// The file content itself is never stored in the database.
/// </summary>
public class ConversionFile
{
    public const int MaxStorageKeyLength = 64;

    private ConversionFile()
    {
    }

    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    public FileRole Role { get; private set; }

    /// <summary>Random server-generated key. The storage layer turns it into a path; clients never see it.</summary>
    public string StorageKey { get; private set; } = null!;

    public long FileSizeBytes { get; private set; }

    public string DetectedFormat { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    // internal: only ConversionJob (same assembly) may create files, so a file
    // can never exist without going through the job's state checks.
    internal static ConversionFile Create(
        Guid jobId,
        FileRole role,
        string storageKey,
        long fileSizeBytes,
        string detectedFormat,
        DateTime nowUtc)
    {
        Guard.RequireText(storageKey, MaxStorageKeyLength, nameof(storageKey));
        Guard.RequireText(detectedFormat, ConversionJob.MaxFormatLength, nameof(detectedFormat));
        Guard.RequireNonNegative(fileSizeBytes, nameof(fileSizeBytes));
        Guard.RequireUtc(nowUtc, nameof(nowUtc));

        return new ConversionFile
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            Role = role,
            StorageKey = storageKey,
            FileSizeBytes = fileSizeBytes,
            DetectedFormat = detectedFormat.ToLowerInvariant(),
            CreatedAtUtc = nowUtc,
        };
    }
}
