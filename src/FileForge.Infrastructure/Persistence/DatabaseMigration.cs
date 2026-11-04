using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Infrastructure.Persistence;

public static class DatabaseMigration
{
    /// <summary>Applies all pending EF Core migrations.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileForgeDbContext>().Database.MigrateAsync(cancellationToken);
    }
}