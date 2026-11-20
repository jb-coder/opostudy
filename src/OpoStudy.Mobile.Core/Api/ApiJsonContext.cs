using System.Text.Json;
using System.Text.Json.Serialization;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>
/// Source-generated JSON metadata for the API contracts (Refit 17 does not use reflection-based JSON).
/// Add every DTO the app sends or receives here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(UserProfileResponse))]
[JsonSerializable(typeof(IReadOnlyList<ExamResponse>))]
[JsonSerializable(typeof(IReadOnlyList<TopicResponse>))]
[JsonSerializable(typeof(MistakeSummaryResponse))]
[JsonSerializable(typeof(IReadOnlyList<QuizSummaryResponse>))]
[JsonSerializable(typeof(CreateQuizRequest))]
[JsonSerializable(typeof(QuizResponse))]
[JsonSerializable(typeof(SubmitAnswerRequest))]
[JsonSerializable(typeof(AnswerFeedbackResponse))]
[JsonSerializable(typeof(StreakResponse))]
[JsonSerializable(typeof(LevelResponse))]
[JsonSerializable(typeof(UpdateDailyGoalRequest))]
[JsonSerializable(typeof(UpdateTimeZoneRequest))]
public sealed partial class ApiJsonContext : JsonSerializerContext;