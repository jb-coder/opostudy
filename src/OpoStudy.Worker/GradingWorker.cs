namespace OpoStudy.Worker;

/// <summary>
/// Will process the grading queue of practical cases (milestone v0.3.0).
/// For now it only starts and waits.
/// </summary>
internal sealed partial class GradingWorker(ILogger<GradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Grading worker started (no queue configured yet)")]
    private static partial void LogStarted(ILogger logger);
}