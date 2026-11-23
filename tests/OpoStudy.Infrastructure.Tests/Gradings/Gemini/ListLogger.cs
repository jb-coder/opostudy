using Microsoft.Extensions.Logging;

namespace OpoStudy.Infrastructure.Tests.Gradings.Gemini;

/// <summary>Keeps every formatted log message (and its exception) so tests can check what would be written.</summary>
internal sealed class ListLogger : ILogger<OpoStudy.Infrastructure.Gradings.Gemini.GeminiLlmCorrector>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
    }
}