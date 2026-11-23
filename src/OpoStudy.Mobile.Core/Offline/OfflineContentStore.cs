using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Mobile.Core.Api;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Stores one JSON payload per exam in SQLite and keeps the deserialized content in memory.</summary>
public sealed partial class OfflineContentStore(
    IOpoStudyApi api,
    OfflineDatabase database,
    TimeProvider time,
    ILogger<OfflineContentStore> logger) : IOfflineContentStore
{
    private readonly ConcurrentDictionary<Guid, OfflineContentResponse> _cache = new();

    public async Task RefreshAsync(Guid examId, CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await ReadVersionAsync(examId, cancellationToken);
            using var response = await api.GetOfflineContentAsync(examId, version is null ? null : $"\"{version}\"", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return;
            }

            if (response is { IsSuccessStatusCode: true, Content: { } content })
            {
                await SaveAsync(content, cancellationToken);
            }
            else
            {
                LogUnexpectedStatus((int?)response.StatusCode);
            }
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogOffline();
        }
    }

    public async Task<OfflineContentResponse?> GetAsync(Guid examId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(examId, out var cached))
        {
            return cached;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM offline_content WHERE exam_id = $examId";
        command.Parameters.AddWithValue("$examId", SqliteValues.ToText(examId));
        if (await command.ExecuteScalarAsync(cancellationToken) is not string payload)
        {
            return null;
        }

        var content = JsonSerializer.Deserialize(payload, ApiJsonContext.Default.OfflineContentResponse);
        if (content is not null)
        {
            _cache[examId] = content;
        }

        return content;
    }

    public async Task<IReadOnlyList<ExamResponse>> ListExamsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<Guid>();
        await using (var connection = await database.OpenAsync(cancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT exam_id FROM offline_content";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(Guid.Parse(reader.GetString(0)));
            }
        }

        var exams = new List<ExamResponse>();
        foreach (var id in ids)
        {
            if (await GetAsync(id, cancellationToken) is { } content)
            {
                exams.Add(content.Exam);
            }
        }

        return [.. exams.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private async Task<string?> ReadVersionAsync(Guid examId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM offline_content WHERE exam_id = $examId";
        command.Parameters.AddWithValue("$examId", SqliteValues.ToText(examId));
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private async Task SaveAsync(OfflineContentResponse content, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO offline_content (exam_id, version, downloaded_at, payload)
            VALUES ($examId, $version, $downloadedAt, $payload)
            ON CONFLICT(exam_id) DO UPDATE SET
                version = excluded.version,
                downloaded_at = excluded.downloaded_at,
                payload = excluded.payload
            """;
        command.Parameters.AddWithValue("$examId", SqliteValues.ToText(content.ExamId));
        command.Parameters.AddWithValue("$version", content.Version);
        command.Parameters.AddWithValue("$downloadedAt", SqliteValues.ToText(time.GetUtcNow()));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(content, ApiJsonContext.Default.OfflineContentResponse));
        await command.ExecuteNonQueryAsync(cancellationToken);
        _cache[content.ExamId] = content;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Offline content not refreshed: no connection")]
    private partial void LogOffline();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Offline content not refreshed: unexpected status {StatusCode}")]
    private partial void LogUnexpectedStatus(int? statusCode);
}