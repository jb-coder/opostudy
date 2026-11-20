namespace OpoStudy.Contracts.Quizzes;

/// <summary>
/// Syncs a quiz taken (fully or partly) offline. The id is chosen by whoever created the quiz, so repeating the
/// request changes nothing. For a quiz that already exists, only <paramref name="QuestionIds"/> and <paramref name="Answers"/> are used.
/// </summary>
/// <param name="Id">Id of the quiz.</param>
/// <param name="ExamId">Exam of the quiz.</param>
/// <param name="TopicId">Topic of a by-topic quiz; <c>null</c> for a random one.</param>
/// <param name="Mode">Quiz mode: <c>ByTopic</c> or <c>Random</c> (a review cannot be created offline).</param>
/// <param name="StartedAt">When the quiz started, according to the device.</param>
/// <param name="QuestionIds">Questions of the quiz, in order.</param>
/// <param name="Answers">Answers given offline and not yet sent.</param>
public sealed record SyncQuizRequest(
    Guid Id,
    Guid ExamId,
    Guid? TopicId,
    string Mode,
    DateTimeOffset StartedAt,
    IReadOnlyList<Guid> QuestionIds,
    IReadOnlyList<SyncAnswerRequest> Answers);
