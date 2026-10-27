using FileForge.Domain.Enums;

namespace FileForge.Domain.Exceptions;

/// <summary>
/// Thrown when an action is not allowed for the job's current status,
/// e.g. completing a job that was never started.
/// </summary>
public sealed class InvalidJobStateException(Guid jobId, JobStatus currentStatus, string action)
    : DomainException($"Cannot {action} job {jobId} in status {currentStatus}.")
{
    public Guid JobId { get; } = jobId;

    public JobStatus CurrentStatus { get; } = currentStatus;

    public string Action { get; } = action;
}
