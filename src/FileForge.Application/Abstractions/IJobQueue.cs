namespace FileForge.Application.Abstractions;

public interface IJobQueue
{
    /// <summary>
    /// Atomically takes the oldest Pending job and moves it to Processing, so no other worker
    /// can take the same job. Returns null if there is nothing to do.
    /// </summary>
    Task<Guid?> TryReserveNextAsync(CancellationToken cancellationToken);
}
