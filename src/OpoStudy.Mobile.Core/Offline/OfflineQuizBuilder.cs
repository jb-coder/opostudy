using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Builds the quiz the screens show from the local quiz, its answers and the cached content. Pure functions.</summary>
internal static class OfflineQuizBuilder
{
    /// <summary>All the questions of the content by id.</summary>
    public static Dictionary<Guid, OfflineQuestionResponse> IndexQuestions(OfflineContentResponse content) =>
        content.Topics.SelectMany(t => t.Questions).GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());

    /// <summary>The feedback of an answer, computed from the cached question.</summary>
    public static AnswerFeedback Feedback(OfflineQuestionResponse question, Guid selectedOptionId) => new(
        selectedOptionId,
        question.Options.Any(o => o.Id == selectedOptionId && o.IsCorrect),
        question.Options.First(o => o.IsCorrect).Id,
        question.Explanation,
        question.LegalReference);

    /// <summary>The quiz response, or <c>null</c> when a question of the quiz is not in the cache anymore.</summary>
    public static QuizResponse? Build(LocalQuiz quiz, IReadOnlyList<LocalAnswer> answers, OfflineContentResponse content)
    {
        var cached = IndexQuestions(content);
        var answersByQuestion = answers.ToDictionary(a => a.QuestionId);
        var questions = new List<QuizQuestionResponse>(quiz.QuestionIds.Count);
        int correct = 0, incorrect = 0;
        foreach (var (questionId, order) in quiz.QuestionIds.Select((id, index) => (id, index)))
        {
            if (!cached.TryGetValue(questionId, out var question))
            {
                return null;
            }

            AnswerFeedback? feedback = null;
            if (answersByQuestion.TryGetValue(questionId, out var answer))
            {
                feedback = Feedback(question, answer.OptionId);
                if (feedback.IsCorrect)
                {
                    correct++;
                }
                else
                {
                    incorrect++;
                }
            }

            questions.Add(new QuizQuestionResponse(
                questionId,
                order,
                question.Statement,
                [.. question.Options.OrderBy(o => o.Order).Select(o => new QuizOptionResponse(o.Id, o.Order, o.Text))],
                feedback,
                false));
        }

        DateTimeOffset? finishedAt = quiz.Finished && answers.Count > 0 ? answers.Max(a => a.AnsweredAt) : null;
        return new QuizResponse(quiz.Id, quiz.ExamId, quiz.TopicId, quiz.Mode, quiz.StartedAt, finishedAt, correct, incorrect, questions);
    }
}