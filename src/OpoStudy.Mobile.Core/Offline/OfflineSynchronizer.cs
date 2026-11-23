using Microsoft.Extensions.Logging;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Flushes the queue when asked or when the connection comes back, showing the progress in <see cref="OfflineState"/>.</summary>
public sealed partial class OfflineSynchronizer : IOfflineSynchronizer, IDisposable
{
    private readonly IOfflineQuizService _quizzes;

    private readonly IQuizSyncService _sync;

    private readonly OfflineState _state;

    private readonly INetworkStatus _network;

    private readonly ILogger<OfflineSynchronizer> _logger;

    private bool _started;

    public OfflineSynchronizer(
        IOfflineQuizService quizzes,
        IQuizSyncService sync,
        OfflineState state,
        INetworkStatus network,
        ILogger<OfflineSynchronizer> logger)
    {
        _quizzes = quizzes;
        _sync = sync;
        _state = state;
        _network = network;
        _logger = logger;
    }

    /// <summary>Starts listening to the connectivity: every time the Internet comes back, the queue is flushed.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _network.Changed += OnNetworkChanged;
    }

    public event EventHandler? ReconnectSynced;

    public void Dispose()
    {
        if (_started)
        {
            _network.Changed -= OnNetworkChanged;
            _started = false;
        }
    }

    public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var syncing = false;
        try
        {
            var pending = await _quizzes.PendingAnswerCountAsync(cancellationToken);
            if (pending > 0 && !_state.IsOffline)
            {
                _state.BeginSync(pending);
                syncing = true;
            }

            var result = await _sync.FlushAsync(null, cancellationToken);
            if (result.SyncedAnswers > 0)
            {
                _state.ReportSuccess();
            }

            _state.ReportDiscardedQuizzes(result.DiscardedQuizzes);
            return result.SyncedAnswers;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSyncFailed(ex, ex.GetType().FullName);
            return 0;
        }
        finally
        {
            if (syncing)
            {
                _state.EndSync();
            }
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        if (_network.HasInternet)
        {
            _ = SyncAfterReconnectAsync();
        }
    }

    private async Task SyncAfterReconnectAsync()
    {
        try
        {
            await SyncAsync();
            RaiseReconnectSynced();
        }
        catch (Exception ex)
        {
            // Fire-and-forget: nothing may escape as an unobserved exception (a subscriber can throw).
            LogReconnectFailed(ex, ex.GetType().FullName);
        }
    }

    /// <summary>Calls every subscriber on its own, so one that throws neither stops the others nor escapes.</summary>
    private void RaiseReconnectSynced()
    {
        foreach (var handler in ReconnectSynced?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogReconnectFailed(ex, ex.GetType().FullName);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconnect handling failed ({ExceptionType})")]
    private partial void LogReconnectFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sync the pending answers ({ExceptionType})")]
    private partial void LogSyncFailed(Exception exception, string? exceptionType);
}
