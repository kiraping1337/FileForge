using FileForge.Application.Abstractions;
using FileForge.Application.Conversions;
using FileForge.Application.Storage;
using FileForge.Infrastructure.Conversion;
using FileForge.Infrastructure.Persistence;
using FileForge.Infrastructure.Queue;
using FileForge.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FileForgeDbContext>(options => options.UseNpgsql(connectionString));

        // Same instance as FileForgeDbContext within a request, not a second context.
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<FileForgeDbContext>());
        services.AddScoped<IJobQueue, PostgresJobQueue>();

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

        return services;
    }
}
