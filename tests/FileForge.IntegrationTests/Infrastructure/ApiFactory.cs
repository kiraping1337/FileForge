using FileForge.Application.Conversions;
using FileForge.Application.Jobs;
using FileForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace FileForge.IntegrationTests.Infrastructure;

/// <summary>
/// The real API, running in memory, on top of a real PostgreSQL in a throwaway Docker container.
/// The worker's services are registered too, so a test can run "one worker step" in-process.
/// Only two things are fake: the clock (tests move time forward) and the converter (no Docker sandbox).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "FileForge.IntegrationTests", Guid.NewGuid().ToString("N"));

    /// <summary>Starts at the real current time; only moves forward when a test says so.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Same migrations as in production: the tests also check that they apply cleanly.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileForgeDbContext>().Database.MigrateAsync();
    }

    // In xUnit v3 IAsyncLifetime includes IAsyncDisposable, which WebApplicationFactory already
    // implements: extend its DisposeAsync instead of adding a second one.
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    /// <summary>Empty database and storage: every test starts from a clean state.</summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "ConversionFiles", "JobAccessTokens", "ConversionJobs" """);

        if (Directory.Exists(_storageRoot))
        {
            foreach (var file in Directory.GetFiles(_storageRoot))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>What the worker does once: take the oldest Pending job and convert it.</summary>
    public async Task<bool> RunWorkerOnceAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ConversionProcessor>()
            .ProcessNextAsync(CancellationToken.None);
    }

    /// <summary>What the cleanup service does on each run.</summary>
    public async Task RunCleanupAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CleanupService>().RunAsync(CancellationToken.None);
    }

    /// <summary>Direct database access for assertions ("what is really stored?").</summary>
    public async Task<T> QueryDbAsync<T>(Func<FileForgeDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<FileForgeDbContext>());
    }

    public int CountStoredFiles() =>
        Directory.Exists(_storageRoot) ? Directory.GetFiles(_storageRoot).Length : 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": no user-secrets, so the tests can never touch your local database.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the connection string
        // before the app is built, and only these settings are visible that early.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Storage:RootPath", _storageRoot);
        builder.UseSetting("UploadRateLimit:PermitLimit", "1000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IFileConverter>();
            services.AddSingleton<IFileConverter, CopyConverter>();

            // Registered by the Worker's Program.cs in production.
            services.AddOptions<CleanupOptions>();
            services.AddScoped<ConversionProcessor>();
            services.AddScoped<CleanupService>();
        });
    }
}
