using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using NpgsqlTypes;

using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

/// <summary>Runs the data backfill of the AddReviewSchedules migration against a database of its own.</summary>
public sealed class ReviewSchedulesBackfillTests(PostgresFixture postgres)
{
    private const string PreviousMigration = "20261126092627_AddMockExams";
    private const string BackfillMigration = "20261126105748_AddReviewSchedules";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Up_WithExistingAnswers_BackfillsOneScheduleFromTheLatestAnswerOfEachQuestion()
    {
        var connectionString = await CreateDatabaseAsync();
        var newYorkUser = Guid.NewGuid();
        var unknownZoneUser = Guid.NewGuid();
        var wrongThenRight = Guid.NewGuid();
        var rightThenWrong = Guid.NewGuid();
        var tie = Guid.NewGuid();
        var unanswered = Guid.NewGuid();
        var lateEvening = Guid.NewGuid();
        var unknownZone = Guid.NewGuid();

        await using (var db = CreateContext(connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration, Ct);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(Ct);
            await InsertUserAsync(connection, newYorkUser, "America/New_York");
            await InsertUserAsync(connection, unknownZoneUser, "Romance Standard Time");

            var t1 = Instant("2026-01-05T10:00:00Z");
            var t2 = Instant("2026-01-06T10:00:00Z");
            var lateNewYork = Instant("2026-01-10T23:30:00-05:00");
            var lateUtc = Instant("2026-01-10T23:30:00Z");

            var quiz1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var quiz2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var quiz3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
            var quiz4 = Guid.Parse("00000000-0000-0000-0000-000000000004");
            await InsertQuizAsync(connection, quiz1, newYorkUser);
            await InsertQuizAsync(connection, quiz2, newYorkUser);
            await InsertQuizAsync(connection, quiz3, newYorkUser);
            await InsertQuizAsync(connection, quiz4, unknownZoneUser);

            // Wrong first, right later: the later answer wins.
            await InsertAnswerAsync(connection, quiz1, wrongThenRight, 0, false, t1);
            await InsertAnswerAsync(connection, quiz2, wrongThenRight, 0, true, t2);

            // Right first, wrong later.
            await InsertAnswerAsync(connection, quiz1, rightThenWrong, 1, true, t1);
            await InsertAnswerAsync(connection, quiz2, rightThenWrong, 1, false, t2);

            // Same answered_at: the greater quiz id wins (quiz 3 is wrong).
            await InsertAnswerAsync(connection, quiz1, tie, 2, true, t1);
            await InsertAnswerAsync(connection, quiz3, tie, 0, false, t1);

            // Never answered: no schedule.
            await InsertAnswerAsync(connection, quiz1, unanswered, 3, null, null);

            // 23:30 local in New York is already the next day in UTC: the local date must be used.
            await InsertAnswerAsync(connection, quiz1, lateEvening, 4, true, lateNewYork);

            // Zone unknown to Postgres: falls back to Europe/Madrid, where 23:30 UTC is already the next day.
            await InsertAnswerAsync(connection, quiz4, unknownZone, 0, false, lateUtc);
        }

        await using (var db = CreateContext(connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration, Ct);
        }

        var rows = await ReadSchedulesAsync(connectionString);

        rows.Count.ShouldBe(5);
        rows[(newYorkUser, wrongThenRight)].ShouldBe((1, new DateOnly(2026, 1, 8)));
        rows[(newYorkUser, rightThenWrong)].ShouldBe((0, new DateOnly(2026, 1, 7)));
        rows[(newYorkUser, tie)].ShouldBe((0, new DateOnly(2026, 1, 6)));
        rows.ContainsKey((newYorkUser, unanswered)).ShouldBeFalse();
        rows[(newYorkUser, lateEvening)].ShouldBe((1, new DateOnly(2026, 1, 12)));
        rows[(unknownZoneUser, unknownZone)].ShouldBe((0, new DateOnly(2026, 1, 12)));
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static OpoStudyDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<OpoStudyDbContext>().UseNpgsql(connectionString);
        OpoStudyDbContext.ApplyConventions(builder);
        return new OpoStudyDbContext(builder.Options);
    }

    /// <summary>
    /// Creates an empty database. Foreign keys are not enforced there (replica role, set on the database so every new
    /// connection gets it): the test only needs the answers, not the exam content they point to.
    /// </summary>
    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"backfill_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync(Ct);
            await using var alter = new NpgsqlCommand($"ALTER DATABASE {name} SET session_replication_role = replica", admin);
            await alter.ExecuteNonQueryAsync(Ct);
        }

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name, Pooling = false }.ConnectionString;
    }

    private static async Task InsertUserAsync(NpgsqlConnection connection, Guid id, string timeZone)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO users (id, firebase_uid, time_zone, created_at, daily_goal, xp, xp_spent) VALUES (@id, @uid, @tz, now(), 20, 0, 0)",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("uid", $"uid-{id:N}");
        command.Parameters.AddWithValue("tz", timeZone);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task InsertQuizAsync(NpgsqlConnection connection, Guid id, Guid userId)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO quiz_attempts (id, user_id, exam_id, mode, revision, started_at) VALUES (@id, @user, @exam, 'Random', 0, now())",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("exam", Guid.NewGuid());
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task InsertAnswerAsync(NpgsqlConnection connection, Guid quizId, Guid questionId, int order, bool? isCorrect, DateTimeOffset? answeredAt)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO quiz_answers (quiz_attempt_id, question_id, \"order\", is_correct, answered_at) VALUES (@quiz, @question, @order, @correct, @at)",
            connection);
        command.Parameters.AddWithValue("quiz", quizId);
        command.Parameters.AddWithValue("question", questionId);
        command.Parameters.AddWithValue("order", order);
        command.Parameters.Add(new NpgsqlParameter("correct", NpgsqlDbType.Boolean) { Value = (object?)isCorrect ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("at", NpgsqlDbType.TimestampTz) { Value = (object?)answeredAt?.UtcDateTime ?? DBNull.Value });
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<Dictionary<(Guid UserId, Guid QuestionId), (int Box, DateOnly DueOn)>> ReadSchedulesAsync(string connectionString)
    {
        var rows = new Dictionary<(Guid UserId, Guid QuestionId), (int Box, DateOnly DueOn)>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT user_id, question_id, box, due_on FROM review_schedules", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            rows[(reader.GetGuid(0), reader.GetGuid(1))] = (reader.GetInt32(2), reader.GetFieldValue<DateOnly>(3));
        }

        return rows;
    }
}