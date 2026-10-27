using FileForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence;

public class FileForgeDbContext(DbContextOptions<FileForgeDbContext> options) : DbContext(options)
{
    public DbSet<ConversionJob> ConversionJobs => Set<ConversionJob>();

    public DbSet<ConversionFile> ConversionFiles => Set<ConversionFile>();

    public DbSet<JobAccessToken> JobAccessTokens => Set<JobAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileForgeDbContext).Assembly);
    }
}
