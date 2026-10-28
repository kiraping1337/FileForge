using FileForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Application.Abstractions;

/// <summary>
/// What the Application layer needs from the database. Implemented by FileForgeDbContext in Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<ConversionJob> ConversionJobs { get; }

    DbSet<ConversionFile> ConversionFiles { get; }

    DbSet<JobAccessToken> JobAccessTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
