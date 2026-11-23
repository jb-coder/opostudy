using Microsoft.Data.Sqlite;

using OpoStudy.Mobile.Core.Offline;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class OfflineDatabaseTests
{
    [Fact]
    public async Task OpenAsync_NewFile_CreatesSchemaV1()
    {
        using var temp = new TempDatabase();

        await using var connection = await temp.Database.OpenAsync(TestContext.Current.CancellationToken);

        (await ScalarAsync(connection, "PRAGMA user_version")).ShouldBe(1L);
        var tables = await ColumnAsync(connection, "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name");
        tables.ShouldBe(["local_quizzes", "offline_content", "pending_answers"]);
    }

    [Fact]
    public async Task OpenAsync_EveryConnection_HasForeignKeysEnabled()
    {
        using var temp = new TempDatabase();

        await using var connection = await temp.Database.OpenAsync(TestContext.Current.CancellationToken);

        (await ScalarAsync(connection, "PRAGMA foreign_keys")).ShouldBe(1L);
    }

    [Fact]
    public async Task OpenAsync_ReopenedExistingFile_KeepsDataAndVersion()
    {
        using var temp = new TempDatabase();
        await using (var first = await temp.Database.OpenAsync(TestContext.Current.CancellationToken))
        {
            await using var insert = first.CreateCommand();
            insert.CommandText = "INSERT INTO offline_content VALUES ('e', 'v', 'now', '{}')";
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = new OfflineDatabase(new OfflineDatabaseOptions(temp.Path));
        await using var connection = await reopened.OpenAsync(TestContext.Current.CancellationToken);

        (await ScalarAsync(connection, "PRAGMA user_version")).ShouldBe(1L);
        (await ScalarAsync(connection, "SELECT COUNT(*) FROM offline_content")).ShouldBe(1L);
    }

    [Fact]
    public async Task OpenAsync_DeletingAQuiz_CascadesToItsAnswers()
    {
        using var temp = new TempDatabase();
        await using var connection = await temp.Database.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, "INSERT INTO local_quizzes VALUES ('q', 'u', 'e', NULL, 'Random', 't', 'now', '[]', 0)");
        await ExecuteAsync(connection, "INSERT INTO pending_answers VALUES ('q', 'a', 'b', 'now', 1, 0)");

        await ExecuteAsync(connection, "DELETE FROM local_quizzes");

        (await ScalarAsync(connection, "SELECT COUNT(*) FROM pending_answers")).ShouldBe(0L);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<string>> ColumnAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}