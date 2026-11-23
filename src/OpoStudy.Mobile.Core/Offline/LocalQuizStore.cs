using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Data.Sqlite;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>A quiz stored in the app (created offline, or adopted from an online one that lost its connection).</summary>
public sealed record LocalQuiz(
    Guid Id,
    string UserId,
    Guid ExamId,
    Guid? TopicId,
    string Mode,
    string Title,
    DateTimeOffset StartedAt,
    IReadOnlyList<Guid> QuestionIds,
    bool Finished);

/// <summary>An answer of a local quiz. <paramref name="Synced"/> answers are already on the server and are never sent.</summary>
public sealed record LocalAnswer(Guid QuestionId, Guid OptionId, DateTimeOffset AnsweredAt, bool IsCorrect, bool Synced);

[JsonSerializable(typeof(Guid[]))]
internal sealed partial class OfflineJsonContext : JsonSerializerContext;

/// <summary>SQL for <c>local_quizzes</c> and <c>pending_answers</c>. Quizzes are always scoped by user.</summary>
public sealed class LocalQuizStore(OfflineDatabase database)
{
    private const string QuizColumns = "id, user_id, exam_id, topic_id, mode, title, started_at, question_ids, finished";

    /// <summary>Inserts the quiz and its answers; does nothing for the ones that already exist.</summary>
    public async Task InsertAsync(LocalQuiz quiz, IReadOnlyList<LocalAnswer> answers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentNullException.ThrowIfNull(answers);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT OR IGNORE INTO local_quizzes ({QuizColumns})
                VALUES ($id, $userId, $examId, $topicId, $mode, $title, $startedAt, $questionIds, $finished)
                """;
            command.Parameters.AddWithValue("$id", SqliteValues.ToText(quiz.Id));
            command.Parameters.AddWithValue("$userId", quiz.UserId);
            command.Parameters.AddWithValue("$examId", SqliteValues.ToText(quiz.ExamId));
            command.Parameters.AddWithValue("$topicId", quiz.TopicId is { } topic ? SqliteValues.ToText(topic) : DBNull.Value);
            command.Parameters.AddWithValue("$mode", quiz.Mode);
            command.Parameters.AddWithValue("$title", quiz.Title);
            command.Parameters.AddWithValue("$startedAt", SqliteValues.ToText(quiz.StartedAt));
            command.Parameters.AddWithValue("$questionIds", JsonSerializer.Serialize(quiz.QuestionIds.ToArray(), OfflineJsonContext.Default.GuidArray));
            command.Parameters.AddWithValue("$finished", quiz.Finished ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var answer in answers)
        {
            await InsertAnswerAsync(connection, transaction, quiz.Id, answer, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>The quiz of that user, or <c>null</c> (also when it belongs to another user).</summary>
    public async Task<LocalQuiz?> GetAsync(Guid quizId, string userId, CancellationToken cancellationToken = default)
    {
        var quizzes = await QueryQuizzesAsync(
            $"SELECT {QuizColumns} FROM local_quizzes WHERE id = $id AND user_id = $userId",
            [("$id", SqliteValues.ToText(quizId)), ("$userId", userId)],
            cancellationToken);
        return quizzes.Count > 0 ? quizzes[0] : null;
    }

    public async Task<IReadOnlyList<LocalAnswer>> GetAnswersAsync(Guid quizId, CancellationToken cancellationToken = default) =>
        await QueryAnswersAsync(quizId, onlyPending: false, cancellationToken);

    /// <summary>The answers still to send, oldest first.</summary>
    public async Task<IReadOnlyList<LocalAnswer>> GetPendingAnswersAsync(Guid quizId, CancellationToken cancellationToken = default) =>
        await QueryAnswersAsync(quizId, onlyPending: true, cancellationToken);

    /// <summary>
    /// Stores a new pending answer and marks the quiz as finished when every question has an answer.
    /// Throws <see cref="InvalidOperationException"/> when the question was already answered.
    /// </summary>
    /// <returns>Whether the quiz is now finished.</returns>
    public async Task<bool> AddAnswerAsync(Guid quizId, LocalAnswer answer, int questionCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(answer);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        if (!await InsertAnswerAsync(connection, transaction, quizId, answer, cancellationToken))
        {
            throw new InvalidOperationException("The question was already answered.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE local_quizzes SET finished = 1
            WHERE id = $id AND (SELECT COUNT(*) FROM pending_answers WHERE quiz_id = $id) >= $questionCount
            """;
        command.Parameters.AddWithValue("$id", SqliteValues.ToText(quizId));
        command.Parameters.AddWithValue("$questionCount", questionCount);
        var finished = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        await transaction.CommitAsync(cancellationToken);
        return finished;
    }

    /// <summary>The most recently started unfinished quiz of the user, or <c>null</c>.</summary>
    public async Task<LocalQuiz?> GetLatestUnfinishedAsync(string userId, CancellationToken cancellationToken = default)
    {
        var quizzes = await QueryQuizzesAsync(
            $"SELECT {QuizColumns} FROM local_quizzes WHERE user_id = $userId AND finished = 0 ORDER BY started_at DESC LIMIT 1",
            [("$userId", userId)],
            cancellationToken);
        return quizzes.Count > 0 ? quizzes[0] : null;
    }

    /// <summary>The quizzes of the user with answers to send, oldest first.</summary>
    public Task<IReadOnlyList<LocalQuiz>> ListWithPendingAnswersAsync(string userId, CancellationToken cancellationToken = default) =>
        QueryQuizzesAsync(
            $"""
            SELECT {QuizColumns} FROM local_quizzes q
            WHERE user_id = $userId AND EXISTS (SELECT 1 FROM pending_answers p WHERE p.quiz_id = q.id AND p.synced = 0)
            ORDER BY started_at
            """,
            [("$userId", userId)],
            cancellationToken);

    public async Task<int> CountPendingAnswersAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM pending_answers p
            JOIN local_quizzes q ON q.id = p.quiz_id
            WHERE q.user_id = $userId AND p.synced = 0
            """;
        command.Parameters.AddWithValue("$userId", userId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Marks the answers of those questions as already on the server.</summary>
    public async Task MarkSyncedAsync(Guid quizId, IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(questionIds);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var questionId in questionIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE pending_answers SET synced = 1 WHERE quiz_id = $quizId AND question_id = $questionId";
            command.Parameters.AddWithValue("$quizId", SqliteValues.ToText(quizId));
            command.Parameters.AddWithValue("$questionId", SqliteValues.ToText(questionId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Deletes the quiz and its answers.</summary>
    public Task DeleteAsync(Guid quizId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM local_quizzes WHERE id = $id", [("$id", SqliteValues.ToText(quizId))], cancellationToken);

    /// <summary>Deletes every quiz of the user (and their answers).</summary>
    public Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM local_quizzes WHERE user_id = $userId", [("$userId", userId)], cancellationToken);

    /// <summary>Deletes the quizzes of any user without answers to send that are finished or started before the cutoff.</summary>
    public Task DeleteFinishedOrOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            DELETE FROM local_quizzes
            WHERE (finished = 1 OR started_at < $cutoff)
              AND NOT EXISTS (SELECT 1 FROM pending_answers p WHERE p.quiz_id = local_quizzes.id AND p.synced = 0)
            """,
            [("$cutoff", SqliteValues.ToText(cutoff))],
            cancellationToken);

    private static async Task<bool> InsertAnswerAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid quizId,
        LocalAnswer answer,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO pending_answers (quiz_id, question_id, option_id, answered_at, is_correct, synced)
            VALUES ($quizId, $questionId, $optionId, $answeredAt, $isCorrect, $synced)
            """;
        command.Parameters.AddWithValue("$quizId", SqliteValues.ToText(quizId));
        command.Parameters.AddWithValue("$questionId", SqliteValues.ToText(answer.QuestionId));
        command.Parameters.AddWithValue("$optionId", SqliteValues.ToText(answer.OptionId));
        command.Parameters.AddWithValue("$answeredAt", SqliteValues.ToText(answer.AnsweredAt));
        command.Parameters.AddWithValue("$isCorrect", answer.IsCorrect ? 1 : 0);
        command.Parameters.AddWithValue("$synced", answer.Synced ? 1 : 0);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<IReadOnlyList<LocalAnswer>> QueryAnswersAsync(Guid quizId, bool onlyPending, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT question_id, option_id, answered_at, is_correct, synced FROM pending_answers
            WHERE quiz_id = $quizId {(onlyPending ? "AND synced = 0" : string.Empty)}
            ORDER BY answered_at, rowid
            """;
        command.Parameters.AddWithValue("$quizId", SqliteValues.ToText(quizId));
        var answers = new List<LocalAnswer>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            answers.Add(new LocalAnswer(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                SqliteValues.ToDate(reader.GetString(2)),
                reader.GetInt32(3) != 0,
                reader.GetInt32(4) != 0));
        }

        return answers;
    }

    private async Task<IReadOnlyList<LocalQuiz>> QueryQuizzesAsync(
        string sql,
        (string Name, object Value)[] parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var quizzes = new List<LocalQuiz>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            quizzes.Add(ReadQuiz(reader));
        }

        return quizzes;
    }

    private static LocalQuiz ReadQuiz(DbDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        Guid.Parse(reader.GetString(2)),
        reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
        reader.GetString(4),
        reader.GetString(5),
        SqliteValues.ToDate(reader.GetString(6)),
        JsonSerializer.Deserialize(reader.GetString(7), OfflineJsonContext.Default.GuidArray) ?? [],
        reader.GetInt32(8) != 0);

    private async Task ExecuteAsync(string sql, (string Name, object Value)[] parameters, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}