using Microsoft.Extensions.Options;

using OpoStudy.Infrastructure.Gradings;

namespace OpoStudy.Worker;

/// <summary>
/// Processes the grading queue of practical cases one element at a time: while there is work it keeps going,
/// and when the queue is empty it sleeps for <see cref="GradingOptions.PollInterval"/>.
/// </summary>
internal sealed partial class GradingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GradingOptions> options,
    ILogger<GradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                // A scope (and DbContext) per iteration, so a failure never leaves a dirty change tracker behind.
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<GradingProcessor>();
                processed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive any unexpected error (database down, bug): it is logged and the next iteration retries.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogUnexpectedError(logger, ex);
            }

            if (!processed)
            {
                try
                {
                    await Task.Delay(options.Value.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Grading worker started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error processing the grading queue")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
