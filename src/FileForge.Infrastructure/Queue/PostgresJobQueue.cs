using FileForge.Application.Abstractions;
using FileForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Queue;

/// <summary>
/// A job queue on top of the ConversionJobs table, no separate broker needed.
/// </summary>
internal sealed class PostgresJobQueue(FileForgeDbContext db, TimeProvider timeProvider) : IJobQueue
{
    public async Task<Guid?> TryReserveNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE locks the row until commit; SKIP LOCKED makes other workers skip rows
        // that are already locked instead of waiting. So two workers never get the same job.
        // 'Pending' is a literal because Status is stored as text (see ConversionJobConfiguration).
        var candidates = await db.ConversionJobs
            .FromSql($"""
                SELECT * FROM "ConversionJobs"
                WHERE "Status" = 'Pending' AND "ExpiresAtUtc" > {now}
                ORDER BY "CreatedAtUtc"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var job = candidates.SingleOrDefault();
        if (job is null)
        {
            return null; // disposing the transaction rolls it back
        }

        job.StartProcessing(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return job.Id;
    }
}
