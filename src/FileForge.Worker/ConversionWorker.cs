using FileForge.Application.Jobs;

namespace FileForge.Worker;

/// <summary>
/// Runs for the whole life of the process: takes jobs one by one, sleeps a little when the queue is empty.
/// </summary>
internal sealed class ConversionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ConversionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Conversion worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            bool processed;

            try
            {
                // The worker itself lives forever, but DbContext must not: a fresh scope
                // (and so a fresh DbContext) for every job, like one HTTP request in the API.
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ConversionProcessor>();
                processed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // E.g. the database is down. Log and keep going instead of killing the process.
                logger.LogError(ex, "Queue iteration failed");
                processed = false;
            }

            if (!processed)
            {
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }
}
