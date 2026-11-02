using FileForge.Application.Conversions;
using FileForge.Application.Jobs;
using FileForge.Infrastructure;
using FileForge.Infrastructure.Conversion;
using FileForge.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' is not configured. Set it in the user-secrets of FileForge.Worker.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<ConversionRegistry>();

builder.Services.AddScoped<ConversionProcessor>();
builder.Services.AddHostedService<ConversionWorker>();

builder.Services.AddOptions<CleanupOptions>()
    .BindConfiguration(CleanupOptions.SectionName)
    .Validate(
        o => o.Interval > TimeSpan.Zero && o.BatchSize > 0 && o.StuckJobTimeout > TimeSpan.Zero && o.MaxAttempts > 0,
        "Invalid 'Cleanup' settings.")
    // A job still being converted must never look stuck, or a second worker could take it.
    .Validate<IOptions<ImageMagickOptions>, IOptions<FfmpegOptions>>(
        (cleanup, imageMagick, ffmpeg) =>
            cleanup.StuckJobTimeout >= 2 * Longest(imageMagick.Value.Timeout, ffmpeg.Value.Timeout),
        "Cleanup:StuckJobTimeout must be at least twice the longest converter timeout.")
    .ValidateOnStart();
builder.Services.AddScoped<JobFileCleaner>();
builder.Services.AddScoped<CleanupService>();
builder.Services.AddHostedService<CleanupWorker>();

builder.Build().Run();

static TimeSpan Longest(TimeSpan a, TimeSpan b) => a > b ? a : b;
