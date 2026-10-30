namespace FileForge.Application.Errors;

/// <summary>
/// Codes the client sees in error responses. Stable strings: the frontend relies on them.
/// </summary>
public static class ErrorCodes
{
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
    public const string InvalidFileContent = "INVALID_FILE_CONTENT";
    public const string ConversionNotSupported = "CONVERSION_NOT_SUPPORTED";
    public const string ProcessingTimeout = "PROCESSING_TIMEOUT";
    public const string ProcessingFailed = "PROCESSING_FAILED";
    public const string JobExpired = "JOB_EXPIRED";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string StorageLimitExceeded = "STORAGE_LIMIT_EXCEEDED";

    // Errors produced by the framework itself rather than by our code.
    public const string ValidationFailed = "VALIDATION_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
    public const string JobNotCompleted = "JOB_NOT_COMPLETED";
    public const string JobInProgress = "JOB_IN_PROGRESS";
}
