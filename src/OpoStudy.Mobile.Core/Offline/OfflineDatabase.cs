using System.Globalization;

using Microsoft.Data.Sqlite;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>
/// Opens connections to the offline SQLite database and creates or upgrades its schema the first time.
/// The schema version lives in <c>PRAGMA user_version</c>; version 1 is created by <c>SchemaV1</c>.
/// </summary>
public sealed class OfflineDatabase(OfflineDatabaseOptions options) : IDisposable
{
    private const int CurrentVersion = 1;

    // pending_answers.synced = 1 marks answers the server already has (adopted from an online quiz, or sent already);
    // they are kept only to know what was answered and are never sent again.
    private const string SchemaV1 = """
        CREATE TABLE IF NOT EXISTS offline_content (
            exam_id TEXT PRIMARY KEY,
            version TEXT NOT NULL,
            downloaded_at TEXT NOT NULL,
            payload TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS local_quizzes (
            id TEXT PRIMARY KEY,
            user_id TEXT NOT NULL,
            exam_id TEXT NOT NULL,
            topic_id TEXT NULL,
            mode TEXT NOT NULL,
            title TEXT NOT NULL,
            started_at TEXT NOT NULL,
            question_ids TEXT NOT NULL,
            finished INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS pending_answers (
            quiz_id TEXT NOT NULL REFERENCES local_quizzes(id) ON DELETE CASCADE,
            question_id TEXT NOT NULL,
            option_id TEXT NOT NULL,
            answered_at TEXT NOT NULL,
            is_correct INTEGER NOT NULL,
            synced INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (quiz_id, question_id));
        """;

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    private bool _schemaReady;

    public void Dispose() => _schemaLock.Dispose();

    /// <summary>Opens a connection with foreign keys enabled. The caller disposes it.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var version = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            if (version < CurrentVersion)
            {
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                await ExecuteAsync(connection, SchemaV1, cancellationToken, transaction);
                await ExecuteAsync(connection, $"PRAGMA user_version = {CurrentVersion};", cancellationToken, transaction);
                await transaction.CommitAsync(cancellationToken);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}