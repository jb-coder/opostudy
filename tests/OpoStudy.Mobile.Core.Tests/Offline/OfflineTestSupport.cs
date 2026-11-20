using System.Net;
using System.Text;

using Microsoft.Data.Sqlite;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Mobile.Core.Offline;

using Refit;

namespace OpoStudy.Mobile.Core.Tests.Offline;

/// <summary>A temporary SQLite file; deleted (after releasing the connection pool) on dispose.</summary>
internal sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"opostudy-test-{Guid.NewGuid():N}.db3");
        Database = new OfflineDatabase(new OfflineDatabaseOptions(Path));
    }

    public string Path { get; }

    public OfflineDatabase Database { get; }

    public void Dispose()
    {
        Database.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm", Path + "-journal" })
        {
            File.Delete(file);
        }
    }
}

internal static class OfflineTestData
{
    public static readonly Guid ExamId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    /// <summary>Content with deterministic ids: topic n has questions with ids ending in n and q (see <see cref="QuestionId"/>).</summary>
    public static OfflineContentResponse Content(int topics = 2, int questionsPerTopic = 3, string version = "v1", Guid? examId = null)
    {
        var id = examId ?? ExamId;
        return new OfflineContentResponse(
            id,
            version,
            new ExamResponse(id, "programador", "Programador C1", "Diputación de Málaga", "C1"),
            [.. Enumerable.Range(1, topics).Select(t => new OfflineTopicResponse(
                TopicId(t),
                t,
                $"Tema {t}",
                null,
                [.. Enumerable.Range(1, questionsPerTopic).Select(q => Question(t, q))]))]);
    }

    public static Guid TopicId(int topic) => new($"22222222-0000-0000-0000-{topic:D12}");

    public static Guid QuestionId(int topic, int question) => new($"33333333-0000-0000-{topic:D4}-{question:D12}");

    /// <summary>Options are numbered 1 to 3 and the second one is the correct one.</summary>
    public static Guid OptionId(int topic, int question, int option) => new($"44444444-{topic:D4}-{question:D4}-0000-{option:D12}");

    public static Guid CorrectOptionId(int topic, int question) => OptionId(topic, question, 2);

    public static Guid WrongOptionId(int topic, int question) => OptionId(topic, question, 1);

    public static OfflineQuestionResponse Question(int topic, int question) => new(
        QuestionId(topic, question),
        $"Pregunta {topic}.{question}",
        $"Aclaración {topic}.{question}",
        $"Art. {question} de la ley {topic}",
        [.. Enumerable.Range(1, 3).Select(o => new OfflineOptionResponse(OptionId(topic, question, o), o, $"Opción {o}", o == 2))]);

    public static ApiResponse<OfflineContentResponse> ContentResponse(HttpStatusCode status, OfflineContentResponse? content) =>
        new(
            new HttpResponseMessage(status) { RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/exams/x/offline-content") },
            content,
            new RefitSettings());

    public static async Task<ApiException> ApiExceptionAsync(HttpStatusCode status, string? code = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/quizzes/sync");
        using var response = new HttpResponseMessage(status);
        if (code is not null)
        {
            response.Content = new StringContent($$"""{"code":"{{code}}","title":"x"}""", Encoding.UTF8, "application/problem+json");
        }

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }
}