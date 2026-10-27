using FileForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything Infrastructure provides. Both the API and (later) the Worker call this,
    /// so the registration lives in one place.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FileForgeDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
