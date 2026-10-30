using FileForge.Application.Jobs;
using Microsoft.Extensions.Options;

namespace FileForge.Worker;

/// <summary>
/// Runs cleanup once at startup (catches leftovers from the previous run) and then on a timer.
/// </summary>
internal sealed class CleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CleanupOptions> options,
    ILogger<CleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cleanup = scope.ServiceProvider.GetRequiredService<CleanupService>();
            await cleanup.RunAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // A failed run must not stop future runs.
            logger.LogError(ex, "Cleanup run failed");
        }
    }
}
