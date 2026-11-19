namespace OpoStudy.Worker;

/// <summary>
/// Procesará la cola de correcciones de supuestos prácticos (milestone v0.3.0-alpha).
/// De momento solo arranca y queda a la espera.
/// </summary>
internal sealed partial class CorreccionesWorker(ILogger<CorreccionesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogArranque(logger);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada ordenada.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker de correcciones iniciado (sin cola configurada todavía)")]
    private static partial void LogArranque(ILogger logger);
}
