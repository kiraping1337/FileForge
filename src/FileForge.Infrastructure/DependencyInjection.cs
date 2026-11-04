using FileForge.Application.Abstractions;
using FileForge.Application.Conversions;
using FileForge.Application.Storage;
using FileForge.Infrastructure.Conversion;
using FileForge.Infrastructure.Persistence;
using FileForge.Infrastructure.Queue;
using FileForge.Infrastructure.Sandbox;
using FileForge.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything: database, queue, storage, converters. Used by the API and the worker.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FileForgeDbContext>(options => options.UseNpgsql(connectionString));

        // Same instance as FileForgeDbContext within a request, not a second context.
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<FileForgeDbContext>());
        services.AddScoped<IJobQueue, PostgresJobQueue>();

        return services.AddConversionSandbox();
    }

    /// <summary>
    /// Storage and converters only, no database. The sandbox runner uses just this:
    /// it has no connection string and could not reach the database even if compromised.
    /// </summary>
    public static IServiceCollection AddConversionSandbox(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName);
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddOptions<SandboxOptions>().BindConfiguration(SandboxOptions.SectionName);
        services.AddOptions<ImageMagickOptions>().BindConfiguration(ImageMagickOptions.SectionName);
        services.AddOptions<FfmpegOptions>().BindConfiguration(FfmpegOptions.SectionName);
        services.AddSingleton<DockerSandbox>();

        // Each converter is registered as IFileConverter; the registry receives all of them at once.
        services.AddSingleton<IFileConverter, ImageMagickConverter>();
        services.AddSingleton<IFileConverter, FfmpegConverter>();
        services.AddSingleton<IFormatDetector, SignatureFormatDetector>();

        // By default conversions run in this process (local development, the runner itself).
        services.TryAddSingleton<IConversionExecutor, LocalConversionExecutor>();

        return services;
    }

    /// <summary>The worker in Docker: hand conversions to the sandbox runner instead of running them here.</summary>
    public static IServiceCollection AddRemoteSandbox(this IServiceCollection services, Uri runnerUrl)
    {
        services.RemoveAll<IConversionExecutor>();
        services.AddHttpClient<IConversionExecutor, RemoteConversionExecutor>(client =>
        {
            client.BaseAddress = runnerUrl;
            // Longer than any converter timeout: the runner enforces those and answers with an error.
            client.Timeout = TimeSpan.FromMinutes(20);
        });

        return services;
    }
}
